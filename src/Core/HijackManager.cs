using System.Collections.Generic;
using AlliedDefenses.Config;
using AlliedDefenses.Networking;
using Unity.Netcode;
using UnityEngine;

namespace AlliedDefenses.Core
{
    /// <summary>A defense currently under our control.</summary>
    public class HijackEntry
    {
        public ulong NetworkId;       // network id of the object (stable across host/clients)
        public string TypeId = "";    // "turret", "mine", ...
        public Component Defense = null!;
        public float ExpireTime;      // Time.time at which control ends (0 = never)
    }

    /// <summary>
    /// Central brain: keeps the list of hijacked defenses, applies/removes the
    /// "allied" state, and handles expiry. Every state change goes through here and
    /// is then broadcast to all players via the networker (never applied locally
    /// only, otherwise multiplayer desyncs).
    ///
    /// Full hijack flow:
    ///   Terminal -> HijackManager.RequestHijack(code)
    ///            -> Networker.RequestHijackServerRpc(netId, typeId)   [client -> server]
    ///            -> Networker.ApplyHijackClientRpc(netId, typeId, true) [server -> everyone]
    ///            -> HijackManager.ApplyHijack(...) on EACH machine
    /// </summary>
    public static class HijackManager
    {
        private static readonly Dictionary<ulong, HijackEntry> _active = new();

        /// <summary>Is the defense (by network id) currently allied?</summary>
        public static bool IsAllied(ulong networkId) => _active.ContainsKey(networkId);

        /// <summary>Convenience overload from the component.</summary>
        public static bool IsAllied(Component defense)
        {
            var netId = ResolveNetworkId(defense);
            return netId.HasValue && _active.ContainsKey(netId.Value);
        }

        /// <summary>Number of currently hijacked defenses.</summary>
        public static int ActiveCount => _active.Count;

        /// <summary>Get the active entry for a network id (or null).</summary>
        public static HijackEntry? Get(ulong networkId) =>
            _active.TryGetValue(networkId, out var e) ? e : null;

        // ----------------------------------------------------------------
        // STEP 1: called locally by the terminal when the player types the
        // command. We resolve the target, then request the network sync.
        // ----------------------------------------------------------------
        public static string RequestHijack(string code)
        {
            if (!DefenseRegistry.TryResolve(code, out var module, out var defense) || defense == null || module == null)
                return $"No defense found for code '{code}'.";

            var netId = ResolveNetworkId(defense);
            if (!netId.HasValue)
                return "This defense has no network identity (cannot be hijacked).";

            var net = HijackNetworker.Active;
            if (net == null)
                return "Network handler not ready yet. Try again in a moment.";

            // Delegate to the networker, which travels server -> all clients.
            net.RequestHijack(netId.Value, module.TypeId);

            float dur = ModConfig.HijackDuration.Value;
            string durText = dur > 0f ? $"for {dur:0} seconds" : "until end of round";
            return $"Hijacking {module.DisplayName} '{code}' {durText}...";
        }

        // ----------------------------------------------------------------
        // LIST every defense of a given type with its terminal id, so you can pick
        // one to hijack/control. Used by "ally turrets" and "ally mines".
        // ----------------------------------------------------------------
        public static string ListDefenses(string typeId)
        {
            var module = DefenseRegistry.FindModule(typeId);
            if (module == null)
                return $"Unknown defense type '{typeId}'.";

            var codes = new List<string>();
            foreach (var obj in UnityEngine.Object.FindObjectsOfType(module.ComponentType))
            {
                if (obj is not Component c) continue;
                codes.Add(TerminalCodeResolver.GetCodeFor(c));
            }
            codes.Sort(System.StringComparer.OrdinalIgnoreCase);

            string name = module.DisplayName.ToLower();
            if (codes.Count == 0)
                return $"No {name}s on this level.";

            return $"{codes.Count} {name}(s) on this level:\n  {string.Join(", ", codes)}\n" +
                   $"Use '{ModConfig.HijackCommand.Value} <id>' to hijack one.";
        }

        // ----------------------------------------------------------------
        // STEP 2: called on EACH machine via ClientRpc. This is where the
        // state actually changes, identically everywhere.
        // ----------------------------------------------------------------
        public static void ApplyHijack(ulong networkId, string typeId, bool allied)
        {
            var defense = ResolveComponent(networkId, typeId);
            var module = DefenseRegistry.FindModule(typeId);
            if (defense == null || module == null)
            {
                Plugin.Log.LogWarning($"ApplyHijack: could not resolve netId={networkId} type={typeId}");
                return;
            }

            module.ApplyAlliedState(defense, allied);

            if (allied)
            {
                float duration = ModConfig.HijackDuration.Value;
                _active[networkId] = new HijackEntry
                {
                    NetworkId = networkId,
                    TypeId = typeId,
                    Defense = defense,
                    ExpireTime = duration > 0f ? Time.time + duration : 0f
                };
                Plugin.Log.LogInfo($"{module.DisplayName} (net {networkId}) is now ALLIED.");
            }
            else
            {
                RadarTimerDisplay.Restore(defense);   // put the plain code back on the map
                _active.Remove(networkId);
                Plugin.Log.LogInfo($"{module.DisplayName} (net {networkId}) is hostile again.");
            }
        }

        // ----------------------------------------------------------------
        // Called every frame (from HijackTicker). Handles expiry and the
        // targeting logic of "passive" defenses (e.g. mines).
        // ----------------------------------------------------------------
        public static void Tick()
        {
            if (_active.Count == 0) return;

            // Only the host decides expiry, then broadcasts the return to hostile.
            bool isServer = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

            List<HijackEntry>? toExpire = null;
            List<ulong>? toRemove = null;

            foreach (var kv in _active)
            {
                var entry = kv.Value;

                // Drop defenses whose Unity object was destroyed (e.g. a mine that
                // exploded). The "== null" here uses Unity's overload, which is true for
                // destroyed objects, so we stop ticking them (was causing NRE spam).
                if (entry.Defense == null)
                {
                    (toRemove ??= new List<ulong>()).Add(entry.NetworkId);
                    continue;
                }

                try
                {
                    // Active targeting for defenses without a native loop (e.g. mine).
                    DefenseRegistry.FindModule(entry.TypeId)?.TickAlliedTargeting(entry.Defense);

                    // Live countdown on the radar map, next to the code box.
                    RadarTimerDisplay.Update(entry);
                }
                catch (System.Exception e)
                {
                    Plugin.Log.LogWarning($"Tick error for {entry.TypeId} (net {entry.NetworkId}); dropping it: {e.Message}");
                    (toRemove ??= new List<ulong>()).Add(entry.NetworkId);
                    continue;
                }

                // A controlled turret still respects the 60s hijack timer (it expires
                // and control ends with it).
                if (isServer && entry.ExpireTime > 0f && Time.time >= entry.ExpireTime)
                    (toExpire ??= new List<HijackEntry>()).Add(entry);
            }

            if (toRemove != null)
                foreach (var id in toRemove)
                    _active.Remove(id);

            if (toExpire != null)
                foreach (var entry in toExpire)
                    HijackNetworker.Active?.RequestUnhijack(entry.NetworkId, entry.TypeId);
        }

        public static void ClearAll()
        {
            foreach (var entry in _active.Values)
            {
                DefenseRegistry.FindModule(entry.TypeId)?.ApplyAlliedState(entry.Defense, false);
                RadarTimerDisplay.Restore(entry.Defense);
            }
            _active.Clear();
        }

        // ----------------------------------------------------------------
        // Network <-> component resolution helpers
        // ----------------------------------------------------------------
        public static ulong? ResolveNetworkId(Component c)
        {
            var no = c.GetComponentInParent<NetworkObject>();
            return no != null ? no.NetworkObjectId : (ulong?)null;
        }

        private static Component? ResolveComponent(ulong networkId, string typeId)
        {
            var sm = NetworkManager.Singleton?.SpawnManager;
            if (sm == null) return null;
            if (!sm.SpawnedObjects.TryGetValue(networkId, out var no) || no == null) return null;

            var module = DefenseRegistry.FindModule(typeId);
            if (module == null) return null;

            // Find the game component (e.g. Turret) on the resolved network object.
            return no.GetComponentInChildren(module.ComponentType);
        }
    }
}

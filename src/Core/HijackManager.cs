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

            if (HijackNetworker.Instance == null)
                return "Network handler not ready yet. Try again in a moment.";

            // Delegate to the networker, which travels server -> all clients.
            HijackNetworker.Instance.RequestHijack(netId.Value, module.TypeId);

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
        // MANUAL CONTROL: take over a turret by id (it gets hijacked first so the
        // auto-targeting bypass is active), then release it.
        // ----------------------------------------------------------------
        public static string RequestControl(string code)
        {
            var defense = TerminalCodeResolver.Resolve(code, typeof(Turret));
            if (defense == null)
                return $"No turret found for code '{code}'. Only turrets can be remote-controlled.";

            var netId = ResolveNetworkId(defense);
            if (!netId.HasValue)
                return "This turret has no network identity (cannot be controlled).";

            if (HijackNetworker.Instance == null)
                return "Network handler not ready yet. Try again in a moment.";

            // Always (re)hijack so the 60s timer is fresh, then take control.
            HijackNetworker.Instance.RequestHijack(netId.Value, "turret");
            HijackNetworker.Instance.RequestControl(netId.Value);

            string releaseKey = ModConfig.ManualControlReleaseKey.Value;
            return $"Taking manual control of turret '{code}'.\n" +
                   $"Watch the ship monitor; aim with the MOUSE, LMB to fire.\n" +
                   $"Press {releaseKey} or type '{ModConfig.HijackCommand.Value} release' to stop.";
        }

        /// <summary>
        /// Take control of the turret NEAREST to the local player (no id needed). Handy
        /// for solo play / testing, where you can't easily read a turret's terminal code.
        /// </summary>
        public static string RequestControlNearest()
        {
            var player = StartOfRound.Instance != null ? StartOfRound.Instance.localPlayerController : null;
            Vector3 origin = player != null ? player.transform.position : Vector3.zero;

            Turret? nearest = null;
            float best = float.MaxValue;
            foreach (var t in UnityEngine.Object.FindObjectsOfType<Turret>())
            {
                float d = (t.transform.position - origin).sqrMagnitude;
                if (d < best) { best = d; nearest = t; }
            }

            if (nearest == null) return "No turret found on this level.";

            var netId = ResolveNetworkId(nearest);
            if (!netId.HasValue) return "Nearest turret has no network identity.";
            if (HijackNetworker.Instance == null) return "Network handler not ready yet. Try again in a moment.";

            // Always (re)hijack so the 60s timer is fresh, then take control.
            HijackNetworker.Instance.RequestHijack(netId.Value, "turret");
            HijackNetworker.Instance.RequestControl(netId.Value);

            string releaseKey = ModConfig.ManualControlReleaseKey.Value;
            return "Taking manual control of the NEAREST turret.\n" +
                   "Watch the ship monitor; aim with the MOUSE, LMB to fire.\n" +
                   $"Press {releaseKey} or type '{ModConfig.HijackCommand.Value} release' to stop.";
        }

        public static string RequestRelease()
        {
            var netId = TurretControlSession.TurretControlledByLocal();
            if (!netId.HasValue)
                return "You are not controlling any turret.";

            HijackNetworker.Instance?.RequestRelease(netId.Value);
            return "Released turret control.";
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
                TurretControlSession.End(networkId);  // stop any manual control when it expires
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

                // Don't let a turret expire while someone is actively controlling it.
                if (isServer && entry.ExpireTime > 0f && Time.time >= entry.ExpireTime
                    && !TurretControlSession.IsControlled(entry.NetworkId))
                    (toExpire ??= new List<HijackEntry>()).Add(entry);
            }

            if (toRemove != null)
                foreach (var id in toRemove)
                    _active.Remove(id);

            if (toExpire != null)
                foreach (var entry in toExpire)
                    HijackNetworker.Instance?.RequestUnhijack(entry.NetworkId, entry.TypeId);
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

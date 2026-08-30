using System;
using AlliedDefenses.Core;
using Unity.Netcode;
using UnityEngine;

namespace AlliedDefenses.Networking
{
    /// <summary>
    /// The mod's shared network component (one instance, spawned by the host).
    ///
    /// Design note (robustness): the host applies every state change DIRECTLY (locally),
    /// and only uses RPCs to mirror it to REMOTE clients. RPC calls are wrapped so a
    /// failure (e.g. an incompletely netcode-patched build) is non-fatal — solo/host play
    /// works no matter what, and multiplayer sync is best-effort. This avoids the
    /// "RPC hash not found" crashes that came from the host invoking RPCs on itself.
    /// </summary>
    public class HijackNetworker : NetworkBehaviour
    {
        public static HijackNetworker? Instance { get; private set; }
        private static bool _warnedRpc;

        /// <summary>
        /// The live networker. Re-acquires it if the cached reference was destroyed
        /// (e.g. after a player disconnects and reconnects, the old object is gone), so
        /// terminal commands keep working on reconnect instead of saying "not ready".
        /// </summary>
        public static HijackNetworker? Active
        {
            get
            {
                if (Instance != null) return Instance; // Unity-null: destroyed -> re-find below
                Instance = UnityEngine.Object.FindObjectOfType<HijackNetworker>();
                return Instance;
            }
        }

        public override void OnNetworkSpawn()
        {
            Instance = this;
            base.OnNetworkSpawn();
            Plugin.Log.LogInfo("HijackNetworker ready (network active).");

            // A client that just joined asks the host for the current team upgrade levels.
            if (!IsServer)
                Safe(() => RequestAllLevelsServerRpc());
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
            base.OnNetworkDespawn();
        }

        private static void Safe(Action rpc)
        {
            try { rpc(); }
            catch (Exception e)
            {
                if (_warnedRpc) return;
                _warnedRpc = true;
                Plugin.Log.LogWarning(
                    "Networking note: an RPC could not be sent. This is harmless in solo " +
                    "and if the build isn't fully netcode-patched; multiplayer sync may be " +
                    $"limited. ({e.Message})");
            }
        }

        // ===================== HIJACK =====================

        public void RequestHijack(ulong netId, string typeId) => ApplyHijack(netId, typeId, true);
        public void RequestUnhijack(ulong netId, string typeId) => ApplyHijack(netId, typeId, false);

        private void ApplyHijack(ulong netId, string typeId, bool allied)
        {
            if (IsServer)
            {
                HijackManager.ApplyHijack(netId, typeId, allied);          // local (always works)
                Safe(() => ApplyHijackClientRpc(netId, typeId, allied));   // mirror to clients
            }
            else
            {
                Safe(() => RequestHijackServerRpc(netId, typeId, allied)); // ask the host
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestHijackServerRpc(ulong netId, string typeId, bool allied)
        {
            HijackManager.ApplyHijack(netId, typeId, allied);
            Safe(() => ApplyHijackClientRpc(netId, typeId, allied));
        }

        [ClientRpc]
        private void ApplyHijackClientRpc(ulong netId, string typeId, bool allied)
        {
            if (IsServer) return; // host already applied locally
            HijackManager.ApplyHijack(netId, typeId, allied);
        }

        // ===================== UPGRADES (team-wide) =====================
        //
        // Upgrades are shared: bought once (from shared ship credits), everyone benefits. After a
        // purchase the buyer calls ShareUpgradeLevel with the new absolute level; the host becomes
        // the source of truth and mirrors it to all clients. Absolute levels make this idempotent,
        // so duplicate/late messages are harmless. Late joiners request the whole set on spawn.

        /// <summary>Share a newly-bought upgrade level with the whole lobby.</summary>
        public void ShareUpgradeLevel(string id, int level)
        {
            if (IsServer)
            {
                UpgradeManager.SetRuntimeLevel(id, level, persistOnHost: true);
                OnHostLevelSet(id, level);
                Safe(() => ShareUpgradeLevelClientRpc(id, level));
            }
            else
            {
                Safe(() => ShareUpgradeLevelServerRpc(id, level));
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void ShareUpgradeLevelServerRpc(string id, int level)
        {
            UpgradeManager.SetRuntimeLevel(id, level, persistOnHost: true);
            OnHostLevelSet(id, level);
            Safe(() => ShareUpgradeLevelClientRpc(id, level));
        }

        [ClientRpc]
        private void ShareUpgradeLevelClientRpc(string id, int level)
        {
            if (IsServer) return; // host already set it
            UpgradeManager.SetRuntimeLevel(id, level);
        }

        // ---- host-authoritative purchases ----
        // Only the host owns the ship terminal, so only the host can change and SYNC the shared
        // credits. A client that bought locally would desync credits and hit "Only the owner can
        // invoke a ServerRpc that requires ownership". So a client asks the host to do the whole
        // purchase against the authoritative credits; the host deducts, applies, and broadcasts.

        /// <summary>Buy something (kind = "upgrade" or "beacon"). Host does it; a client asks the host.</summary>
        public void RequestPurchase(string kind, string id)
        {
            if (IsServer) DoHostPurchase(kind, id);
            else Safe(() => RequestPurchaseServerRpc(kind, id ?? ""));
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestPurchaseServerRpc(string kind, string id) => DoHostPurchase(kind, id);

        private void DoHostPurchase(string kind, string id)
        {
            var terminal = ShipCredits.Find();
            if (terminal == null) return;
            int credits = ShipCredits.Get(terminal);

            if (kind == "beacon")
            {
                var (_, spent) = Beacon.BeaconManager.Purchase(credits);
                if (spent > 0)
                {
                    ShipCredits.Set(terminal, credits - spent);
                    UpgradeManager.SetRuntimeLevel("beacon", UpgradeManager.LevelOf("beacon"), persistOnHost: true);
                    Safe(() => ShareUpgradeLevelClientRpc("beacon", UpgradeManager.LevelOf("beacon")));
                }
            }
            else // upgrade
            {
                var (_, spent) = UpgradeManager.Buy(id, credits);
                if (spent > 0)
                {
                    ShipCredits.Set(terminal, credits - spent);
                    Safe(() => ShareUpgradeLevelClientRpc(id, UpgradeManager.LevelOf(id)));
                }
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestAllLevelsServerRpc()
        {
            // Re-broadcast every current level so the new client (and, harmlessly, everyone else)
            // ends up in sync.
            foreach (var (id, level) in UpgradeManager.AllRuntimeLevels())
                Safe(() => ShareUpgradeLevelClientRpc(id, level));
        }

        /// <summary>Host-side side effects of a level arriving. The beacon is deployed manually, so
        /// we only need to remove it when ownership is reset to 0.</summary>
        private static void OnHostLevelSet(string id, int level)
        {
            if (id == "beacon" && level <= 0)
                Beacon.BeaconManager.DespawnAll();
        }

        // ---- beacon deploy / recall (host-authoritative) ----

        /// <summary>Deploy (or move) the beacon at a world position. Host does it; a client asks.</summary>
        public void RequestDeploy(Vector3 pos)
        {
            if (IsServer) Beacon.BeaconManager.DeployAt(pos);
            else Safe(() => RequestDeployServerRpc(pos));
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestDeployServerRpc(Vector3 pos) => Beacon.BeaconManager.DeployAt(pos);

        /// <summary>Recall (despawn) the deployed beacon.</summary>
        public void RequestRecall()
        {
            if (IsServer) Beacon.BeaconManager.DespawnAll();
            else Safe(() => RequestRecallServerRpc());
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestRecallServerRpc() => Beacon.BeaconManager.DespawnAll();
    }
}

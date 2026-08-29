using System;
using AlliedDefenses.Core;
using Unity.Netcode;

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

        [ServerRpc(RequireOwnership = false)]
        private void RequestAllLevelsServerRpc()
        {
            // Re-broadcast every current level so the new client (and, harmlessly, everyone else)
            // ends up in sync.
            foreach (var (id, level) in UpgradeManager.AllRuntimeLevels())
                Safe(() => ShareUpgradeLevelClientRpc(id, level));
        }

        /// <summary>Host-side side effects of a level arriving (e.g. deliver the beacon it unlocks).</summary>
        private static void OnHostLevelSet(string id, int level)
        {
            if (id == "beacon" && level > 0)
                Beacon.BeaconManager.SpawnIfMissing(); // a client bought the beacon -> host delivers it
        }
    }
}

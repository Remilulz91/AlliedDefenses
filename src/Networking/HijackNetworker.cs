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
    }
}

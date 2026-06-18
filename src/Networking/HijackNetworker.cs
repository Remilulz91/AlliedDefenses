using AlliedDefenses.Core;
using Unity.Netcode;
using UnityEngine;

namespace AlliedDefenses.Networking
{
    /// <summary>
    /// The mod's shared network component. A single instance exists in game (spawned
    /// by the host). It is the "pipe" through which every state change travels: a
    /// client requests a hijack -> the host validates -> all clients apply it. This
    /// keeps the defense state identical for everyone.
    ///
    /// ⚠️ IMPORTANT: for the RPCs below to work, the .dll must be run through the
    /// "Netcode Patcher" after compilation (see README, NETWORKING section). Without
    /// it, the RPCs are not wired up and will do nothing.
    /// </summary>
    public class HijackNetworker : NetworkBehaviour
    {
        public static HijackNetworker? Instance { get; private set; }

        public override void OnNetworkSpawn()
        {
            Instance = this;
            base.OnNetworkSpawn();
            Plugin.Log.LogInfo("HijackNetworker ready (network active).");
        }

        // -------- API called locally by HijackManager --------

        public void RequestHijack(ulong netId, string typeId) =>
            RequestHijackServerRpc(netId, typeId, true);

        public void RequestUnhijack(ulong netId, string typeId) =>
            RequestHijackServerRpc(netId, typeId, false);

        // -------- Network round trip --------

        // Client -> Server. RequireOwnership=false because any player may request it.
        [ServerRpc(RequireOwnership = false)]
        private void RequestHijackServerRpc(ulong netId, string typeId, bool allied)
        {
            // Extension point: server-side validation (credits, global cooldown,
            // anti-abuse...). If rejected -> do not re-broadcast. For now we accept.
            ApplyHijackClientRpc(netId, typeId, allied);
        }

        // Server -> ALL clients (host included). This is where state changes everywhere.
        [ClientRpc]
        private void ApplyHijackClientRpc(ulong netId, string typeId, bool allied)
        {
            HijackManager.ApplyHijack(netId, typeId, allied);
        }

        // =====================================================================
        //  MANUAL REMOTE CONTROL
        //  Same client -> server -> all pattern. Begin/End set who is driving;
        //  Aim streams the controller's look direction + fire state to everyone so
        //  the turret moves and shoots identically on all screens.
        // =====================================================================

        public void RequestControl(ulong netId) =>
            BeginControlServerRpc(netId, NetworkManager.Singleton.LocalClientId);

        public void RequestRelease(ulong netId) => EndControlServerRpc(netId);

        public void SendAim(ulong netId, Vector3 dir, bool firing) =>
            AimServerRpc(netId, dir, firing);

        [ServerRpc(RequireOwnership = false)]
        private void BeginControlServerRpc(ulong netId, ulong clientId)
        {
            // Only one controller per turret: ignore if already taken.
            if (TurretControlSession.IsControlled(netId)) return;
            BeginControlClientRpc(netId, clientId);
        }

        [ClientRpc]
        private void BeginControlClientRpc(ulong netId, ulong clientId) =>
            TurretControlSession.Begin(netId, clientId);

        [ServerRpc(RequireOwnership = false)]
        private void EndControlServerRpc(ulong netId) => EndControlClientRpc(netId);

        [ClientRpc]
        private void EndControlClientRpc(ulong netId) => TurretControlSession.End(netId);

        // Streamed every frame by the controller. Cheap payload (vector + bool).
        // TODO (optimization): throttle to ~20-30 Hz if bandwidth becomes a concern.
        [ServerRpc(RequireOwnership = false)]
        private void AimServerRpc(ulong netId, Vector3 dir, bool firing) =>
            AimClientRpc(netId, dir, firing);

        [ClientRpc]
        private void AimClientRpc(ulong netId, Vector3 dir, bool firing) =>
            TurretControlSession.SetAim(netId, dir, firing);
    }
}

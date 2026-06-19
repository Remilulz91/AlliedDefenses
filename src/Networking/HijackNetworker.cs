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

        public override void OnNetworkSpawn()
        {
            Instance = this;
            base.OnNetworkSpawn();
            Plugin.Log.LogInfo("HijackNetworker ready (network active).");
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

        // ===================== MANUAL CONTROL =====================

        public void RequestControl(ulong netId) =>
            SetControl(netId, NetworkManager.Singleton.LocalClientId, true);

        public void RequestRelease(ulong netId) =>
            SetControl(netId, NetworkManager.Singleton.LocalClientId, false);

        private void SetControl(ulong netId, ulong clientId, bool begin)
        {
            if (IsServer)
            {
                ApplyControlLocal(netId, clientId, begin);
                Safe(() => ControlClientRpc(netId, clientId, begin));
            }
            else
            {
                Safe(() => RequestControlServerRpc(netId, clientId, begin));
            }
        }

        private static void ApplyControlLocal(ulong netId, ulong clientId, bool begin)
        {
            if (begin)
            {
                if (TurretControlSession.IsControlled(netId)) return; // one controller at a time
                TurretControlSession.Begin(netId, clientId);
            }
            else
            {
                TurretControlSession.End(netId);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestControlServerRpc(ulong netId, ulong clientId, bool begin)
        {
            ApplyControlLocal(netId, clientId, begin);
            Safe(() => ControlClientRpc(netId, clientId, begin));
        }

        [ClientRpc]
        private void ControlClientRpc(ulong netId, ulong clientId, bool begin)
        {
            if (IsServer) return; // host already applied locally
            ApplyControlLocal(netId, clientId, begin);
        }

        // ===================== AIM STREAM =====================
        // The controller applies its own aim locally every frame (ManualControlInput),
        // so this only needs to mirror it to the OTHER players.

        public void SendAim(ulong netId, Vector3 dir, bool firing)
        {
            if (IsServer) Safe(() => AimClientRpc(netId, dir, firing));
            else Safe(() => AimServerRpc(netId, dir, firing));
        }

        [ServerRpc(RequireOwnership = false)]
        private void AimServerRpc(ulong netId, Vector3 dir, bool firing)
        {
            TurretControlSession.SetAim(netId, dir, firing);
            Safe(() => AimClientRpc(netId, dir, firing));
        }

        [ClientRpc]
        private void AimClientRpc(ulong netId, Vector3 dir, bool firing)
        {
            if (IsServer) return; // host already applied locally
            TurretControlSession.SetAim(netId, dir, firing);
        }
    }
}

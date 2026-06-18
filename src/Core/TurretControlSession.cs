using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace AlliedDefenses.Core
{
    /// <summary>One turret currently under manual remote control.</summary>
    public class ControlInfo
    {
        public ulong TurretNetId;
        public ulong ControllerClientId;       // which player is driving it
        public Vector3 AimDirection = Vector3.forward; // world-space direction the barrel should face
        public bool Firing;                    // is the controller holding fire?
    }

    /// <summary>
    /// Tracks which turrets are being manually driven, by whom, and where they are
    /// aiming. Like HijackManager, this state is the same on every machine because it
    /// is only ever changed through the networker's RPCs (begin / end / aim).
    ///
    /// Manual control is layered ON TOP of hijacking: a turret must be allied first
    /// (so the auto-targeting patch is already bypassing vanilla); when a ControlInfo
    /// exists for it, TurretHijack.DriveAlliedTurret switches from auto-targeting to
    /// "do exactly what the controller's aim/fire says".
    /// </summary>
    public static class TurretControlSession
    {
        private static readonly Dictionary<ulong, ControlInfo> _byTurret = new();

        public static bool IsControlled(ulong netId) => _byTurret.ContainsKey(netId);

        public static ControlInfo? Get(ulong netId) =>
            _byTurret.TryGetValue(netId, out var c) ? c : null;

        /// <summary>The turret the LOCAL player is currently driving (or null).</summary>
        public static ulong? TurretControlledByLocal()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return null;
            foreach (var kv in _byTurret)
                if (kv.Value.ControllerClientId == nm.LocalClientId) return kv.Key;
            return null;
        }

        // ---- applied identically on all machines via ClientRpc ----

        public static void Begin(ulong netId, ulong controllerClientId)
        {
            _byTurret[netId] = new ControlInfo
            {
                TurretNetId = netId,
                ControllerClientId = controllerClientId
            };
            Plugin.Log.LogInfo($"Turret (net {netId}) is now manually controlled by client {controllerClientId}.");
        }

        public static void End(ulong netId)
        {
            if (_byTurret.Remove(netId))
                Plugin.Log.LogInfo($"Turret (net {netId}) manual control ended.");
        }

        public static void SetAim(ulong netId, Vector3 dir, bool firing)
        {
            if (_byTurret.TryGetValue(netId, out var c))
            {
                c.AimDirection = dir;
                c.Firing = firing;
            }
        }
    }
}

using AlliedDefenses.Config;
using AlliedDefenses.Monitor;
using AlliedDefenses.Networking;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AlliedDefenses.Core
{
    /// <summary>
    /// Local input for manual turret control. You watch the turret's view on the ship
    /// monitor (OpenBodyCams main body cam, retargeted to the turret) and aim with the
    /// MOUSE; left-mouse fires. Release with the configured key (default V) or the
    /// "ally release" command.
    ///
    /// If OpenBodyCams isn't installed there's no remote view, but the turret still
    /// responds to the mouse (you'd aim it while standing near it).
    /// </summary>
    public class ManualControlInput : MonoBehaviour
    {
        private ulong? _current;
        private float _yaw;
        private float _pitch;
        private float _nextSend;

        // Mouse delta is already per-frame (pixels), so we do NOT multiply by deltaTime.
        // 0.08 * sensitivity(default 2) ~= 0.16 deg per pixel; raise sensitivity in the
        // config if you want faster aiming.
        private const float MouseScale = 0.08f;
        private const float SendInterval = 0.05f; // broadcast aim to other players at ~20 Hz

        private void Update()
        {
            var netId = TurretControlSession.TurretControlledByLocal();

            if (!netId.HasValue)
            {
                if (_current.HasValue) // just stopped controlling
                {
                    TurretBodyCam.Hide();
                    _current = null;
                }
                return;
            }

            if (_current != netId) // just started controlling this turret
            {
                _current = netId;
                BeginControl(netId.Value);
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                float sens = ModConfig.ManualControlSensitivity.Value;
                Vector2 d = mouse.delta.ReadValue();
                _yaw += d.x * sens * MouseScale;
                _pitch = Mathf.Clamp(_pitch - d.y * sens * MouseScale, -80f, 80f);

                Vector3 dir = Quaternion.Euler(_pitch, _yaw, 0f) * Vector3.forward;
                bool firing = mouse.leftButton.isPressed;

                // Apply locally right away: responsive aim + correct fire on/off, and it
                // works in solo even if the network broadcast hiccups.
                TurretControlSession.SetAim(netId.Value, dir, firing);

                // Broadcast to the other players, throttled, and never let a network
                // hiccup spam the log or break control.
                if (Time.time >= _nextSend)
                {
                    _nextSend = Time.time + SendInterval;
                    try { HijackNetworker.Instance?.SendAim(netId.Value, dir, firing); }
                    catch (System.Exception e) { Plugin.Log.LogWarning($"SendAim failed: {e.Message}"); }
                }
            }

            if (TryGetReleaseKey(out var key) && Keyboard.current != null &&
                Keyboard.current[key].wasPressedThisFrame)
            {
                HijackNetworker.Instance?.RequestRelease(netId.Value);
            }
        }

        private void BeginControl(ulong netId)
        {
            var sm = NetworkManager.Singleton?.SpawnManager;
            if (sm == null || !sm.SpawnedObjects.TryGetValue(netId, out var no) || no == null) return;

            var turret = no.GetComponentInChildren<Turret>();
            if (turret == null || turret.turretRod == null ||
                turret.aimPoint == null || turret.centerPoint == null) return;

            // Seed yaw/pitch from the current barrel direction so the aim doesn't snap.
            Vector3 dir = (turret.aimPoint.position - turret.centerPoint.position).normalized;
            if (dir.sqrMagnitude > 1e-5f)
            {
                _yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                _pitch = -Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;
            }

            // Show the turret's view on the ship monitor (via OpenBodyCams, if installed).
            TurretBodyCam.Show(turret.turretRod, turret.centerPoint, turret.aimPoint);
        }

        private static bool TryGetReleaseKey(out Key key)
        {
            return System.Enum.TryParse(ModConfig.ManualControlReleaseKey.Value, true, out key);
        }
    }
}

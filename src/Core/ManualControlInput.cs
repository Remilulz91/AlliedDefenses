using AlliedDefenses.Config;
using AlliedDefenses.Monitor;
using AlliedDefenses.Networking;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AlliedDefenses.Core
{
    /// <summary>
    /// Reads the LOCAL player's input while they remote-control a turret, and streams
    /// the aim direction + fire state to everyone through the networker.
    ///
    /// While controlling, mouse movement turns the turret (yaw/pitch), left mouse fires,
    /// and the release key (default V) hands control back. When the player is watching
    /// the ship monitor (which shows the turret's gun-cam, see TurretMonitorFeed), this
    /// feels like operating the turret remotely.
    ///
    /// ⚠️ In-engine tuning needed: depending on game state the player's own look/move
    /// input may still be active. You will likely want to freeze the controller's
    /// movement and possibly hide the cursor while driving. Those hooks are marked TODO
    /// and are best finished while testing in-game.
    /// </summary>
    public class ManualControlInput : MonoBehaviour
    {
        private ulong? _current;   // turret net id we are currently driving
        private float _yaw;
        private float _pitch;

        private void Update()
        {
            var netId = TurretControlSession.TurretControlledByLocal();

            // Not controlling anything: if we just stopped, restore the monitor and bail.
            if (!netId.HasValue)
            {
                if (_current.HasValue)
                {
                    TurretMonitorFeed.Hide();
                    _current = null;
                    // TODO: restore local player movement / cursor here.
                }
                return;
            }

            // Just started controlling this turret: seed yaw/pitch from its current facing
            // so the camera/turret doesn't snap, and switch the monitor to the gun-cam.
            if (_current != netId)
            {
                _current = netId;
                SeedFromTurret(netId.Value);
                TurretMonitorFeed.Show(netId.Value);
                // TODO: freeze local player movement here, hide cursor, etc.
            }

            var mouse = Mouse.current;
            if (mouse == null) return;

            float sens = ModConfig.ManualControlSensitivity.Value;
            Vector2 d = mouse.delta.ReadValue();
            _yaw += d.x * sens * Time.deltaTime;
            _pitch -= d.y * sens * Time.deltaTime;
            _pitch = Mathf.Clamp(_pitch, -80f, 80f);

            Vector3 dir = Quaternion.Euler(_pitch, _yaw, 0f) * Vector3.forward;
            bool firing = mouse.leftButton.isPressed;

            HijackNetworker.Instance?.SendAim(netId.Value, dir, firing);

            // Release control with the configured key. The actual teardown (monitor +
            // state) happens next frame once the networked End reaches us and
            // TurretControlledByLocal() returns null (handled above).
            if (TryGetReleaseKey(out var key) && Keyboard.current != null &&
                Keyboard.current[key].wasPressedThisFrame)
            {
                HijackNetworker.Instance?.RequestRelease(netId.Value);
            }
        }

        private void SeedFromTurret(ulong netId)
        {
            var sm = NetworkManager.Singleton?.SpawnManager;
            if (sm == null || !sm.SpawnedObjects.TryGetValue(netId, out var no) || no == null) return;

            var turret = no.GetComponentInChildren<Turret>();
            Transform? pivot = turret != null
                ? (turret.centerPoint != null ? turret.centerPoint : turret.transform)
                : null;
            if (pivot == null) return;

            Vector3 e = pivot.rotation.eulerAngles;
            _yaw = e.y;
            _pitch = NormalizePitch(e.x);
        }

        private static float NormalizePitch(float x) => x > 180f ? x - 360f : x;

        private static bool TryGetReleaseKey(out Key key)
        {
            return System.Enum.TryParse(ModConfig.ManualControlReleaseKey.Value, true, out key);
        }
    }
}

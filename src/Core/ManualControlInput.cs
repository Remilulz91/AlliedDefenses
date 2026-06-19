using AlliedDefenses.Config;
using AlliedDefenses.Networking;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AlliedDefenses.Core
{
    /// <summary>
    /// While the local player controls a turret, the turret simply follows WHERE THE
    /// PLAYER LOOKS: each frame we send the player's camera forward direction as the
    /// aim, and left-mouse as fire. No camera swap, no render-texture — you keep your
    /// normal first-person view and watch the turret turn and shoot in the world.
    ///
    /// Release with the configured key (default V) or the "ally release" command.
    /// </summary>
    public class ManualControlInput : MonoBehaviour
    {
        private void Update()
        {
            var netId = TurretControlSession.TurretControlledByLocal();
            if (!netId.HasValue) return;

            var player = StartOfRound.Instance != null ? StartOfRound.Instance.localPlayerController : null;
            if (player == null || player.gameplayCamera == null) return;

            // Aim = the player's look direction; fire = left mouse button.
            Vector3 dir = player.gameplayCamera.transform.forward;
            bool firing = Mouse.current != null && Mouse.current.leftButton.isPressed;
            HijackNetworker.Instance?.SendAim(netId.Value, dir, firing);

            // Release control with the configured key.
            if (TryGetReleaseKey(out var key) && Keyboard.current != null &&
                Keyboard.current[key].wasPressedThisFrame)
            {
                HijackNetworker.Instance?.RequestRelease(netId.Value);
            }
        }

        private static bool TryGetReleaseKey(out Key key)
        {
            return System.Enum.TryParse(ModConfig.ManualControlReleaseKey.Value, true, out key);
        }
    }
}

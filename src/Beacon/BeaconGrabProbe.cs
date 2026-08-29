using HarmonyLib;
using UnityEngine;
using AlliedDefenses.Config;

namespace AlliedDefenses.Beacon
{
    /// <summary>
    /// TEMPORARY diagnostic. When dev commands are enabled, this replays the EXACT grab detection
    /// the game does (same ray, same mask 0x40000340, same line-of-sight linecast on layer 30) and
    /// logs what it finds, so we can see why the grab prompt never appears. Attached to each beacon
    /// instance; remove once grabbing works. Throttled to ~1.3 Hz and silent unless it hits something.
    /// </summary>
    public class BeaconGrabProbe : MonoBehaviour
    {
        private const int InteractableMask = 1073742656; // PlayerControllerB.interactableObjectsMask
        private const int RoomMask = 1073741824;         // 1<<30, used by the LOS linecast
        private float _t;

        private void Update()
        {
            if (!ModConfig.EnableDevCommands.Value) return;
            _t += Time.deltaTime;
            if (_t < 0.75f) return;
            _t = 0f;

            var sor = StartOfRound.Instance;
            var player = sor != null ? sor.localPlayerController : null;
            var cam = player != null ? player.gameplayCamera : null;
            if (cam == null) return;

            var ray = new Ray(cam.transform.position, cam.transform.forward);
            if (!Physics.Raycast(ray, out var hit, 5f, InteractableMask))
                return; // aiming at nothing in range - stay quiet

            var g = hit.collider.GetComponentInParent<GrabbableObject>();
            Plugin.Log.LogInfo(
                $"[GrabProbe] hit='{hit.collider.name}' layer={hit.collider.gameObject.layer} " +
                $"tag={hit.collider.tag} dist={hit.distance:0.0} grabbable={(g != null)}");

            if (g != null)
            {
                bool losBlocked = Physics.Linecast(cam.transform.position, g.transform.position,
                    RoomMask, QueryTriggerInteraction.Collide);
                int slot = -99;
                try { slot = player.FirstEmptyItemSlot(); } catch { }
                bool gameStarted = false;
                try { gameStarted = Traverse.Create(sor).Field("gameHasStarted").GetValue<bool>(); } catch { }
                Plugin.Log.LogInfo(
                    $"[GrabProbe]   LOSblocked={losBlocked} firstEmptySlot={slot} " +
                    $"canGrabBeforeStart={(g.itemProperties != null && g.itemProperties.canBeGrabbedBeforeGameStart)} " +
                    $"gameHasStarted={gameStarted} isHeld={g.isHeld}");
            }
        }
    }
}

using UnityEngine;
using UnityEngine.InputSystem;
using AlliedDefenses.Config;
using AlliedDefenses.Networking;

namespace AlliedDefenses.Core
{
    /// <summary>
    /// The Hack Tool: once bought ('ally hack'), aim at a locked big door, turret, mine or spike
    /// trap and press the hack key to trigger it - exactly like typing its code at the ship terminal,
    /// but from inside the facility. The action is routed to the host, which runs the game's own
    /// CallFunctionFromTerminal (fires the object's networked open/disable event), so it's reliable.
    /// </summary>
    public class HackToolInput : MonoBehaviour
    {
        private Key _key = Key.H;
        private bool _resolved;
        private float _cooldownUntil;

        private void Update()
        {
            if (!ModConfig.EnableHackTool.Value) return;

            var kb = Keyboard.current;
            if (kb == null) return;
            if (!_resolved) { _key = ParseKey(ModConfig.HackKey.Value, Key.H); _resolved = true; }
            if (!kb[_key].wasPressedThisFrame) return;
            if (Time.unscaledTime < _cooldownUntil) return;

            var sor = StartOfRound.Instance;
            var p = sor != null ? sor.localPlayerController : null;
            if (p == null || !p.isPlayerControlled || p.isPlayerDead) return;
            if (p.inTerminalMenu || p.isTypingChat) return;

            if (!UpgradeManager.HackToolOwned)
            {
                Tip("You don't own the Hack Tool - buy it with 'ally hack'.");
                return;
            }

            var cam = p.gameplayCamera;
            if (cam == null) return;

            float range = Mathf.Max(1f, ModConfig.HackRange.Value);
            if (Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit, range,
                                ~0, QueryTriggerInteraction.Collide))
            {
                var tao = hit.collider.GetComponentInParent<TerminalAccessibleObject>()
                       ?? hit.collider.GetComponentInChildren<TerminalAccessibleObject>();
                if (tao != null)
                {
                    _cooldownUntil = Time.unscaledTime + 0.5f;
                    HijackNetworker.Active?.RequestHack(tao);
                    Tip("Hack Tool: triggered.");
                    return;
                }
            }
            Tip("Aim at a locked door, turret, mine or spike trap (in range).");
        }

        private static Key ParseKey(string name, Key fallback) =>
            System.Enum.TryParse((name ?? "").Trim(), ignoreCase: true, out Key k) ? k : fallback;

        private static void Tip(string body)
        {
            try { HUDManager.Instance?.DisplayTip("Hack Tool", body, isWarning: false, useSave: false, prefsKey: "LC_Tip1"); }
            catch { }
        }
    }
}

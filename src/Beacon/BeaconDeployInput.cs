using UnityEngine;
using UnityEngine.InputSystem;
using AlliedDefenses.Config;
using AlliedDefenses.Core;
using AlliedDefenses.Networking;

namespace AlliedDefenses.Beacon
{
    /// <summary>
    /// Watches the configured deploy key (Input System). When pressed by a player who owns the
    /// beacon, it asks the host to deploy/move the beacon to that player's position. Ignored while
    /// typing in the terminal or chat, dead, or not in control.
    /// </summary>
    public class BeaconDeployInput : MonoBehaviour
    {
        private Key _deployKey = Key.B;
        private Key _recallKey = Key.N;
        private bool _resolved;
        private float _cooldownUntil;

        private void Update()
        {
            if (!ModConfig.EnableBeacon.Value) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            if (!_resolved)
            {
                _deployKey = ParseKey(ModConfig.BeaconDeployKey.Value, Key.B);
                _recallKey = ParseKey(ModConfig.BeaconRecallKey.Value, Key.N);
                _resolved = true;
            }

            bool deploy = kb[_deployKey].wasPressedThisFrame;
            bool recall = kb[_recallKey].wasPressedThisFrame;
            if (!deploy && !recall) return;
            if (Time.unscaledTime < _cooldownUntil) return;

            var sor = StartOfRound.Instance;
            var p = sor != null ? sor.localPlayerController : null;
            if (p == null || !p.isPlayerControlled || p.isPlayerDead) return;
            if (p.inTerminalMenu || p.isTypingChat) return; // don't fire while typing

            if (!UpgradeManager.BeaconOwned)
            {
                Tip("You don't own a beacon yet - buy it with 'ally beacon'.");
                return;
            }

            _cooldownUntil = Time.unscaledTime + 0.4f;
            if (recall)
            {
                HijackNetworker.Active?.RequestRecall();
                Tip("Defense Beacon recalled (stored).");
            }
            else
            {
                HijackNetworker.Active?.RequestDeploy(p.transform.position);
                Tip("Defense Beacon deployed here.");
            }
        }

        private static Key ParseKey(string name, Key fallback) =>
            System.Enum.TryParse((name ?? "").Trim(), ignoreCase: true, out Key k) ? k : fallback;

        private static void Tip(string body)
        {
            try { HUDManager.Instance?.DisplayTip("Defense Beacon", body, isWarning: false, useSave: false, prefsKey: "LC_Tip1"); }
            catch { }
        }
    }
}

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
        private Key _key = Key.B;
        private bool _resolved;

        private void Update()
        {
            if (!ModConfig.EnableBeacon.Value) return;

            var kb = Keyboard.current;
            if (kb == null) return;

            if (!_resolved)
            {
                var name = (ModConfig.BeaconDeployKey.Value ?? "B").Trim();
                if (!System.Enum.TryParse(name, ignoreCase: true, out _key)) _key = Key.B;
                _resolved = true;
            }

            if (!kb[_key].wasPressedThisFrame) return;

            var sor = StartOfRound.Instance;
            var p = sor != null ? sor.localPlayerController : null;
            if (p == null || !p.isPlayerControlled || p.isPlayerDead) return;
            if (p.inTerminalMenu || p.isTypingChat) return; // don't fire while typing

            if (!UpgradeManager.BeaconOwned)
            {
                Tip("You don't own a beacon yet - buy it with 'ally beacon'.");
                return;
            }

            HijackNetworker.Active?.RequestDeploy(p.transform.position);
            Tip("Defense Beacon deployed here.");
        }

        private static void Tip(string body)
        {
            try { HUDManager.Instance?.DisplayTip("Defense Beacon", body, isWarning: false, useSave: false, prefsKey: "LC_Tip1"); }
            catch { }
        }
    }
}

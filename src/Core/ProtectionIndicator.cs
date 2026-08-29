using UnityEngine;

namespace AlliedDefenses.Core
{
    /// <summary>
    /// A small "you are protected" cue for the local player. When the local player steps into an
    /// active counter-play aura (within the largest active aura radius of an allied defense or a
    /// beacon), we show a brief HUD tip (which also plays the game's soft tip sound). Purely
    /// client-side and cosmetic; a cooldown avoids spam if you walk in and out.
    ///
    /// Only fires if at least one player-centred aura is actually owned (MaxAuraRadius > 0), so
    /// players who haven't bought any of those upgrades never see it.
    /// </summary>
    public static class ProtectionIndicator
    {
        private static bool _inside;
        private static float _cooldownUntil;

        public static void Tick()
        {
            float radius = UpgradeManager.MaxAuraRadius();
            if (radius <= 0f) { _inside = false; return; }

            var sor = StartOfRound.Instance;
            var player = sor != null ? sor.localPlayerController : null;
            if (player == null || !player.isPlayerControlled || player.isPlayerDead) { _inside = false; return; }

            bool now = HijackManager.AnyAlliedWithin(player.transform.position, radius);

            if (now && !_inside && Time.time >= _cooldownUntil)
            {
                _cooldownUntil = Time.time + 12f; // don't nag if you cross the edge repeatedly
                try
                {
                    HUDManager.Instance?.DisplayTip(
                        "Allied defense", "You are shielded by an allied aura.",
                        isWarning: false, useSave: false, prefsKey: "LC_Tip1");
                }
                catch { /* HUD not ready -> ignore */ }
            }

            _inside = now;
        }
    }
}

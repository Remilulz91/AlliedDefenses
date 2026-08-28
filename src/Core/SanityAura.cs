using UnityEngine;
using AlliedDefenses.Config;

namespace AlliedDefenses.Core
{
    /// <summary>
    /// Ghost Girl counter-play. The Ghost Girl (DressGirlAI) feeds on a hidden per-player
    /// "insanity" value (PlayerControllerB.insanityLevel): it rises when you are alone or
    /// in the dark, and she targets / escalates on the most insane player.
    ///
    /// This does NOT try to fight her AI directly (she is immune to stun and ethereal).
    /// Instead, while the LOCAL player stands within radius of an ALLIED defense, we bleed
    /// their insanity down — the same effect as sticking near a teammate. It suppresses and
    /// delays her rather than being an off switch, which keeps it balanced (a deliberate
    /// design choice: it makes the hijacks more tactical, not a hard counter).
    ///
    /// Runs on every client for its own local player (insanity is a client-side value),
    /// so no networking is needed. Gated behind the 'sanity' upgrade (level 0 = off).
    /// </summary>
    public static class SanityAura
    {
        public static void Tick()
        {
            if (!UpgradeManager.SanityAuraEnabled) return;

            var sor = StartOfRound.Instance;
            var player = sor != null ? sor.localPlayerController : null;
            if (player == null || !player.isPlayerControlled || player.isPlayerDead) return;

            float radius = UpgradeManager.SanityAuraRadius();
            if (radius <= 0f) return;
            if (!HijackManager.AnyAlliedWithin(player.transform.position, radius)) return;

            float rate = UpgradeManager.SanityAuraRate();
            if (rate <= 0f) return;

            player.insanityLevel = Mathf.Max(0f, player.insanityLevel - rate * Time.deltaTime);
        }
    }
}

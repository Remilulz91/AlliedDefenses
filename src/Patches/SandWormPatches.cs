using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;
using AlliedDefenses.Core;

namespace AlliedDefenses.Patches
{
    /// <summary>
    /// Earth Leviathan (SandWormAI) counter-play — "seismic cloak".
    ///
    /// The worm is UNKILLABLE and does not hunt by sound; its targeting (DoAIInterval) simply
    /// picks the closest player via EnemyAI.GetClosestPlayer, which internally filters every
    /// candidate through EnemyAI.PlayerIsTargetable. So instead of fighting the worm, we make a
    /// protected player invisible to it: this prefix forces PlayerIsTargetable to return false
    /// — but ONLY when the caller is a SandWormAI (scoped via the type check) and ONLY for a
    /// player standing inside an allied-defense radius (a placed beacon around the ship).
    ///
    /// Effect chain: GetClosestPlayer skips the protected player (never chosen as target); if the
    /// worm was already chasing them, its Update sees them become untargetable and it drops the
    /// chase (SwitchToBehaviourState(0)). Other enemies are unaffected because of the is-check.
    ///
    /// Runs where GetClosestPlayer runs (the worm's owner / host), which is exactly where the
    /// authoritative targeting decision is made. Gated behind the 'seismic' upgrade (0 = off).
    /// </summary>
    [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.PlayerIsTargetable))]
    internal static class SandWormUntargetablePatch
    {
        [HarmonyPrefix]
        private static bool MakeCloakedPlayerUntargetable(
            EnemyAI __instance, PlayerControllerB playerScript, ref bool __result)
        {
            if (!UpgradeManager.SeismicEnabled) return true;      // upgrade off -> vanilla
            if (!(__instance is SandWormAI)) return true;         // only the worm is fooled
            if (playerScript == null) return true;

            float radius = UpgradeManager.SeismicRadius();
            if (radius <= 0f) return true;

            if (HijackManager.AnyAlliedWithin(playerScript.transform.position, radius))
            {
                __result = false;   // "not a valid target"
                return false;       // skip the original check
            }
            return true;            // outside the cloak -> normal targeting
        }
    }
}

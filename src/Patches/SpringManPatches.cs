using HarmonyLib;
using UnityEngine;
using AlliedDefenses.Core;

namespace AlliedDefenses.Patches
{
    /// <summary>
    /// Keeps a Coil-Head frozen while an allied turret is neutralizing it. Runs AFTER the
    /// game's SpringManAI.Update (postfix), so whatever speed the game just set is overridden
    /// back to 0 — the Coil-Head can't advance. We only touch the NavMeshAgent speed (not the
    /// animator), so once the neutralize expires the game's own Update restores normal
    /// movement on the next frame with nothing left in a stuck state.
    /// </summary>
    [HarmonyPatch(typeof(SpringManAI), "Update")]
    internal static class SpringManFreezePatch
    {
        [HarmonyPostfix]
        private static void ForceFreeze(SpringManAI __instance)
        {
            if (!CoilheadNeutralizer.IsNeutralized(__instance)) return;
            try
            {
                if (__instance.agent != null && __instance.agent.isActiveAndEnabled && __instance.agent.isOnNavMesh)
                {
                    __instance.agent.speed = 0f;
                    __instance.agent.velocity = Vector3.zero;
                }
            }
            catch { /* never let counter-play crash the enemy update */ }
        }
    }
}

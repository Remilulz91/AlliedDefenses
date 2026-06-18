using AlliedDefenses.Config;
using AlliedDefenses.Core;
using HarmonyLib;
using UnityEngine;

namespace AlliedDefenses.Patches
{
    /// <summary>
    /// Harmony patches on the game's Landmine class.
    ///
    /// Their only job is to make an ALLIED mine safe for players: while the mine is
    /// hijacked, the vanilla player-trigger path is blocked, so walking over it never
    /// arms or detonates it. Enemy detonation is handled separately by
    /// MineHijack.TickAlliedTargeting.
    ///
    /// A non-allied mine is untouched and behaves exactly like vanilla.
    /// </summary>
    [HarmonyPatch(typeof(Landmine))]
    public static class MinePatches
    {
        // OnTriggerEnter(Collider other) -> vanilla arms the mine on "Player".
        [HarmonyPrefix]
        [HarmonyPatch("OnTriggerEnter")]
        public static bool OnTriggerEnterPrefix(Landmine __instance, Collider other)
        {
            if (ShouldProtectPlayers(__instance) && other.CompareTag("Player"))
                return false; // skip vanilla: an allied mine ignores players
            return true;
        }

        // OnTriggerExit(Collider other) -> vanilla explodes the mine on "Player" exit.
        [HarmonyPrefix]
        [HarmonyPatch("OnTriggerExit")]
        public static bool OnTriggerExitPrefix(Landmine __instance, Collider other)
        {
            if (ShouldProtectPlayers(__instance) && other.CompareTag("Player"))
                return false; // skip vanilla: never blow up under a player
            return true;
        }

        private static bool ShouldProtectPlayers(Landmine mine) =>
            ModConfig.IgnorePlayersWhenAllied.Value && HijackManager.IsAllied(mine);
    }
}

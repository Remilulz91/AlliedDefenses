using AlliedDefenses.Config;
using AlliedDefenses.Core;
using HarmonyLib;
using UnityEngine;

namespace AlliedDefenses.Patches
{
    /// <summary>
    /// Harmony patch on the game's SpikeRoofTrap (the ceiling spike trap).
    ///
    /// Its only job is to make an ALLIED spike trap safe for players: while hijacked,
    /// the vanilla damage path is skipped for player colliders, so it won't crush you.
    /// Enemy colliders are untouched, so caught enemies are still crushed.
    ///
    /// A non-allied spike trap is left exactly as vanilla.
    /// </summary>
    [HarmonyPatch(typeof(SpikeRoofTrap))]
    public static class SpikePatches
    {
        // OnTriggerStay(Collider other) -> where the vanilla trap applies its damage.
        [HarmonyPrefix]
        [HarmonyPatch("OnTriggerStay")]
        public static bool OnTriggerStayPrefix(SpikeRoofTrap __instance, Collider other)
        {
            if (ModConfig.IgnorePlayersWhenAllied.Value
                && HijackManager.IsAllied(__instance)
                && other != null && other.CompareTag("Player"))
                return false; // allied spike: ignore players (no crush)

            return true; // otherwise vanilla behaviour
        }
    }
}

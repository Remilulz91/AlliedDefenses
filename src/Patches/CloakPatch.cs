using GameNetcodeStuff;
using AlliedDefenses.Core;

namespace AlliedDefenses.Patches
{
    /// <summary>
    /// "Sensor cloak" counter-play. PlayerControllerB implements IVisibleThreat.GetVisibility, which
    /// the game's detection-based enemies (RadMechAI / Old Bird, Forest Giant, Baboon Hawk, Kidnapper
    /// Fox, Giant Kiwi) read to decide whether they can see the player. The game already returns 0 for
    /// a DEAD player, and the enemies handle that gracefully (they ignore them). So while the player is
    /// inside an allied-defense/beacon radius we force the visibility to 0 too — making them invisible
    /// to those enemies, including the unkillable Old Bird.
    ///
    /// This is an EXPLICIT interface method, so it's patched manually in Plugin.Awake (not by
    /// attribute). Gated behind the 'cloak' upgrade (level 0 = off).
    /// </summary>
    internal static class CloakPatch
    {
        public static void Postfix(PlayerControllerB __instance, ref float __result)
        {
            if (!UpgradeManager.CloakEnabled) return;
            if (__instance == null) return;

            float r = UpgradeManager.CloakRadius();
            if (r <= 0f) return;

            if (HijackManager.AnyAlliedWithin(__instance.transform.position, r))
                __result = 0f; // "not visible" - the same value the game uses for a dead player
        }
    }
}

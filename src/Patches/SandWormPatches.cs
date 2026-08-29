using GameNetcodeStuff;
using HarmonyLib;
using AlliedDefenses.Core;

namespace AlliedDefenses.Patches
{
    /// <summary>
    /// "Untargetable" counter-play for unkillable enemies that pick the closest player through
    /// EnemyAI.PlayerIsTargetable. Instead of fighting them, we make a protected player invisible
    /// to their targeting while that player stands inside an allied-defense radius (a hijacked
    /// defense or a placed beacon). The prefix forces PlayerIsTargetable to return false, scoped by
    /// the enemy TYPE so nothing else is affected:
    ///
    ///   - Earth Leviathan / sand worm (SandWormAI), gated by the 'seismic' upgrade. It targets via
    ///     GetClosestPlayer -> PlayerIsTargetable, so a cloaked player is never chosen (and dropped
    ///     if already chased).
    ///   - Barber (ClaySurgeonAI), gated by the 'barber' upgrade. It "dances" toward the closest
    ///     targetable player via TargetClosestPlayer -> PlayerIsTargetable, so a cloaked player is
    ///     not jumped at.
    ///   - Hygrodere / slime (BlobAI), gated by the 'slime' upgrade. Same TargetClosestPlayer path;
    ///     with no targetable player it just roams, so the slow blob wanders off instead of chasing.
    ///   - Circuit Bees (RedLocustBees), gated by the 'bees' upgrade. Their line-of-sight aggro near
    ///     the hive isn't blocked, but their CHASE state validates the target via PlayerIsTargetable,
    ///     so a cloaked player makes them drop the chase (weaker: disengage, not full stealth).
    ///
    /// Runs where the targeting decision is made (the enemy's owner / host). Both effects are pure
    /// suppression (they hide you), not an off switch: an enemy already mid-lunge can still connect.
    /// </summary>
    [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.PlayerIsTargetable))]
    internal static class UntargetableAuraPatch
    {
        [HarmonyPrefix]
        private static bool MakeCloakedPlayerUntargetable(
            EnemyAI __instance, PlayerControllerB playerScript, ref bool __result)
        {
            if (playerScript == null) return true;

            float radius;
            if (__instance is SandWormAI && UpgradeManager.SeismicEnabled)
                radius = UpgradeManager.SeismicRadius();
            else if (__instance is ClaySurgeonAI && UpgradeManager.BarberEnabled)
                radius = UpgradeManager.BarberRadius();
            else if (__instance is BlobAI && UpgradeManager.SlimeEnabled)
                radius = UpgradeManager.SlimeRadius();
            else if (__instance is RedLocustBees && UpgradeManager.BeesEnabled)
                radius = UpgradeManager.BeesRadius();
            else
                return true; // not a covered enemy, or its upgrade is off -> vanilla targeting

            if (radius > 0f && HijackManager.AnyAlliedWithin(playerScript.transform.position, radius))
            {
                __result = false;   // "not a valid target"
                return false;       // skip the original check
            }
            return true;            // outside the cloak -> normal targeting
        }
    }
}

using AlliedDefenses.Core;
using AlliedDefenses.Defenses;
using HarmonyLib;

namespace AlliedDefenses.Patches
{
    /// <summary>
    /// Harmony patches on the game's Turret class.
    ///
    /// Chosen strategy (robust): when a turret is ALLIED, we SHORT-CIRCUIT its native
    /// Update() loop (which targets and fires at players) and run our own allied logic
    /// instead (aim/fire at enemies). This means we do not depend on the names of the
    /// turret's internal targeting methods: we replace the whole behavior while the
    /// turret is on our side.
    ///
    /// Trade-off: we re-implement aiming and firing (see TurretHijack.DriveAlliedTurret),
    /// so some native animations/sounds are temporarily lost. A finer alternative (for
    /// later) would be to surgically patch only the player-detection method so it
    /// returns an enemy instead — prettier but tied to internal names. See README,
    /// "Going further".
    /// </summary>
    [HarmonyPatch(typeof(Turret))]
    public static class TurretPatches
    {
        // Shared module instance (used to run the aiming logic).
        private static readonly TurretHijack _module = new();

        // private void Update()  -> patched by name to stay flexible.
        [HarmonyPrefix]
        [HarmonyPatch("Update")]
        public static bool UpdatePrefix(Turret __instance)
        {
            // Not hijacked: let the game do its usual thing.
            if (!HijackManager.IsAllied(__instance))
                return true; // run the original Update

            // Allied turret: custom behavior, then skip the original Update.
            _module.DriveAlliedTurret(__instance);
            return false; // do NOT run the original Update (so no firing at players)
        }
    }
}

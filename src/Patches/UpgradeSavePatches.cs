using HarmonyLib;
using AlliedDefenses.Core;

namespace AlliedDefenses.Patches
{
    /// <summary>
    /// Loads the per-save upgrade levels when a save slot loads (PerSave mode only;
    /// no-op in Persistent mode). Runs after the vanilla StartOfRound.Start, at which
    /// point GameNetworkManager.currentSaveFileName is already set.
    /// </summary>
    [HarmonyPatch(typeof(StartOfRound), "Start")]
    internal static class UpgradeSaveLoadPatch
    {
        [HarmonyPostfix]
        private static void LoadUpgradesForSave()
        {
            UpgradeManager.LoadForCurrentSave();
        }
    }

    /// <summary>
    /// Postfix target for GameNetworkManager.ResetSavedGameValues (a game over / save
    /// wipe). Patched MANUALLY and guarded in Plugin.Awake, so if a game update renames
    /// the method the rest of the mod keeps working. PerSave mode wipes our stored
    /// levels here; Persistent mode ignores it.
    /// </summary>
    internal static class UpgradeGameOverPatch
    {
        internal static void AfterReset()
        {
            UpgradeManager.OnGameOverReset();
        }
    }
}

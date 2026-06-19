using System;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using AlliedDefenses.Core;
using AlliedDefenses.Config;

namespace AlliedDefenses
{
    /// <summary>
    /// Mod entry point, loaded by BepInEx when the game starts.
    ///
    /// Responsibilities:
    ///   1. Register with BepInEx (the [BepInPlugin] attribute).
    ///   2. Load the configuration.
    ///   3. Apply every Harmony patch (the code that modifies the game).
    ///   4. Register the defense modules and start the runtime ticker.
    ///
    /// Kept intentionally small: all real logic lives in the other files. This
    /// class just wires the pieces together.
    /// </summary>
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        // --- Mod identity (keep in sync with manifest.json) ---
        // Author: Remilulz_91
        public const string Author = "Remilulz_91";
        public const string Guid = "Remilulz_91.AlliedDefenses";
        public const string Name = "AlliedDefenses";
        public const string Version = "0.2.6";

        /// <summary>Singleton instance, accessible anywhere via Plugin.Instance.</summary>
        public static Plugin Instance { get; private set; } = null!;

        /// <summary>Shared logger: Plugin.Log.LogInfo("...").</summary>
        public static ManualLogSource Log { get; private set; } = null!;

        private readonly Harmony _harmony = new Harmony(Guid);

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            // 1) Configuration (command keyword, duration, range, etc.)
            //    base.Config is BepInEx's ConfigFile (qualified to avoid any clash
            //    with the AlliedDefenses.Config namespace).
            ModConfig.Init(base.Config);

            // 2) Apply ALL Harmony patches found in this assembly.
            //    Harmony automatically scans classes marked with [HarmonyPatch].
            try
            {
                _harmony.PatchAll();

                // 3) Register the available defense modules (turret, +mine later).
                DefenseRegistry.RegisterDefaults();

                // 4) Start the ticker (hijack expiry + passive-defense targeting).
                HijackTicker.EnsureExists();

                Log.LogInfo($"{Name} v{Version} by {Author} loaded. {DefenseRegistry.Count} defense module(s) active.");
            }
            catch (Exception e)
            {
                Log.LogError($"Failed to apply Harmony patches: {e}");
            }
        }
    }
}

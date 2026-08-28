using System;
using System.Collections.Generic;
using System.Text;
using AlliedDefenses.Config;
using BepInEx.Configuration;
using UnityEngine;

namespace AlliedDefenses.Core
{
    /// <summary>
    /// Buy-with-credits progression for the allied defenses (like a ship-upgrade tree).
    ///
    /// The current level of each upgrade lives in memory (<see cref="Upgrade.RuntimeLevel"/>)
    /// and is the single source of truth for gameplay. HOW it is stored depends on the
    /// config option <see cref="ModConfig.UpgradePersistence"/>:
    ///
    ///   Persistent : stored in the BepInEx config file. Survives EVERYTHING (deaths,
    ///                game overs, new save files). Reset only via 'ally upgrade reset'.
    ///                This is the default (no rage-quit reset of your progress).
    ///
    ///   PerSave    : stored in the game's own save slot (ES3), keyed by save file name.
    ///                Kept through deaths, but wiped on a GAME OVER (the game resets the
    ///                slot) and separate per save slot. This mirrors the game's progress.
    ///
    /// The HOST owns the levels and drives all gameplay effects (damage, expiry), so no
    /// heavy network sync is needed for correctness.
    /// </summary>
    public static class UpgradeManager
    {
        public sealed class Upgrade
        {
            public string Id = "";
            public string Name = "";
            public int MaxLevel;
            public int BaseCost;       // cost of the first level
            public float CostGrowth;   // cost multiplier per already-owned level
            public int RuntimeLevel;   // the level in effect right now (source of truth)
            public ConfigEntry<int> Level = null!; // backing store for the Persistent mode
            public Func<int, string> Describe = _ => "";
        }

        private static readonly List<Upgrade> _upgrades = new();
        private static readonly Dictionary<string, Upgrade> _byId = new(StringComparer.OrdinalIgnoreCase);

        private static bool PerSave => ModConfig.UpgradePersistence.Value == UpgradeSaveMode.PerSave;

        // --- effective values used by the rest of the mod ---

        /// <summary>Hijack duration (seconds) with the duration upgrade applied.</summary>
        public static float EffectiveDuration()
        {
            float baseDur = ModConfig.HijackDuration.Value;
            if (baseDur <= 0f) return 0f; // 0 = unlimited, upgrades don't matter
            if (!ModConfig.EnableUpgrades.Value) return baseDur;
            return baseDur + LevelOf("duration") * 20f; // +20s per level
        }

        /// <summary>Turret damage per shot to enemies, with the damage upgrade applied.</summary>
        public static int EffectiveTurretDamage()
        {
            int baseDmg = ModConfig.TurretEnemyDamage.Value;
            if (!ModConfig.EnableUpgrades.Value) return baseDmg;
            return baseDmg + LevelOf("turretdamage"); // +1 per level
        }

        // ----------------------------------------------------------------

        public static void Init(ConfigFile cfg)
        {
            _upgrades.Clear();
            _byId.Clear();

            Add(cfg, "duration", "Hijack duration", maxLevel: 10, baseCost: 120, growth: 1.5f,
                describe: lvl => $"{ModConfig.HijackDuration.Value + lvl * 20:0}s allied");

            Add(cfg, "turretdamage", "Turret damage", maxLevel: 15, baseCost: 100, growth: 1.4f,
                describe: lvl => $"{ModConfig.TurretEnemyDamage.Value + lvl} dmg/shot");
        }

        private static void Add(ConfigFile cfg, string id, string name, int maxLevel, int baseCost,
                                float growth, Func<int, string> describe)
        {
            var u = new Upgrade
            {
                Id = id,
                Name = name,
                MaxLevel = maxLevel,
                BaseCost = baseCost,
                CostGrowth = growth,
                Describe = describe,
                Level = cfg.Bind("Upgrades (saved)", id + "Level", 0,
                    $"Level of the '{name}' upgrade in Persistent mode. In PerSave mode it is stored in the game save instead."),
            };
            // In Persistent mode the config value is the starting truth; in PerSave mode we
            // start at 0 and load the real value when a save slot loads (StartOfRound.Start).
            u.RuntimeLevel = PerSave ? 0 : u.Level.Value;
            _upgrades.Add(u);
            _byId[id] = u;
        }

        public static int LevelOf(string id) => _byId.TryGetValue(id, out var u) ? u.RuntimeLevel : 0;

        /// <summary>Cost of the NEXT level, or -1 if maxed / unknown.</summary>
        public static int NextCost(Upgrade u)
        {
            if (u.RuntimeLevel >= u.MaxLevel) return -1;
            return Mathf.RoundToInt(u.BaseCost * Mathf.Pow(u.CostGrowth, u.RuntimeLevel));
        }

        // ----------------------------------------------------------------
        //  Storage backends
        // ----------------------------------------------------------------

        private static string Es3Key(Upgrade u) => "AlliedDefenses_" + u.Id + "Level";

        /// <summary>The current save slot file name, or null if none is loaded.</summary>
        private static string SaveFileName()
        {
            try
            {
                var gnm = GameNetworkManager.Instance;
                return gnm != null ? gnm.currentSaveFileName : null;
            }
            catch { return null; }
        }

        /// <summary>Write one upgrade's level to whichever backend is active.</summary>
        private static void Persist(Upgrade u)
        {
            if (!PerSave)
            {
                u.Level.Value = u.RuntimeLevel; // BepInEx writes the config file
                return;
            }
            string file = SaveFileName();
            if (file == null) return; // no slot loaded (shouldn't happen from the in-game terminal)
            try { ES3.Save(Es3Key(u), u.RuntimeLevel, file); }
            catch (Exception e) { Plugin.Log.LogWarning($"Upgrade save (PerSave) failed for '{u.Id}': {e.Message}"); }
        }

        /// <summary>
        /// PerSave only: load the levels for the save slot that just loaded. Called on
        /// StartOfRound.Start. In Persistent mode this is a no-op.
        /// </summary>
        public static void LoadForCurrentSave()
        {
            if (!PerSave) return;
            string file = SaveFileName();
            foreach (var u in _upgrades)
            {
                int lvl = 0;
                if (file != null)
                {
                    try { lvl = ES3.Load(Es3Key(u), file, 0); }
                    catch (Exception e) { Plugin.Log.LogWarning($"Upgrade load (PerSave) failed for '{u.Id}': {e.Message}"); }
                }
                u.RuntimeLevel = Mathf.Clamp(lvl, 0, u.MaxLevel);
            }
        }

        /// <summary>
        /// PerSave only: the game wiped the save slot (game over) -> clear our stored levels.
        /// Called from the guarded ResetSavedGameValues hook. In Persistent mode this is a no-op.
        /// </summary>
        public static void OnGameOverReset()
        {
            if (!PerSave) return;
            string file = SaveFileName();
            foreach (var u in _upgrades)
            {
                u.RuntimeLevel = 0;
                if (file == null) continue;
                try { if (ES3.KeyExists(Es3Key(u), file)) ES3.DeleteKey(Es3Key(u), file); }
                catch (Exception e) { Plugin.Log.LogWarning($"Upgrade wipe (PerSave) failed for '{u.Id}': {e.Message}"); }
            }
            Plugin.Log.LogInfo("AlliedDefenses upgrades reset for this save slot (game over).");
        }

        // ----------------------------------------------------------------
        //  Terminal commands
        // ----------------------------------------------------------------

        /// <summary>Text for "ally upgrades": every upgrade, its level and next cost.</summary>
        public static string ListText(int credits)
        {
            if (!ModConfig.EnableUpgrades.Value)
                return "Upgrades are disabled in the config.";

            var sb = new StringBuilder();
            sb.AppendLine("ALLIED DEFENSES - UPGRADES");
            sb.AppendLine("------------------------------------");
            sb.AppendLine($"Credits: {credits}");
            string mode = PerSave ? "per save (reset on game over)" : "kept through game over";
            sb.AppendLine($"Save mode: {mode}");
            sb.AppendLine("");
            foreach (var u in _upgrades)
            {
                int cost = NextCost(u);
                string costText = cost < 0 ? "MAX" : $"{cost} cr";
                sb.AppendLine($"{u.Id,-12} Lv {u.RuntimeLevel}/{u.MaxLevel}  ({u.Describe(u.RuntimeLevel)})  next: {costText}");
            }
            sb.AppendLine("");
            sb.AppendLine($"Buy with '{ModConfig.HijackCommand.Value} upgrade <id>'.");
            return sb.ToString();
        }

        /// <summary>
        /// Buy the next level of an upgrade, paying from the ship's credits.
        /// Returns (message, creditsSpent). creditsSpent is 0 if nothing was bought.
        /// </summary>
        public static (string message, int spent) Buy(string id, int credits)
        {
            if (!ModConfig.EnableUpgrades.Value)
                return ("Upgrades are disabled in the config.", 0);
            if (!_byId.TryGetValue(id, out var u))
                return ($"Unknown upgrade '{id}'. Type '{ModConfig.HijackCommand.Value} upgrades' to see them.", 0);

            int cost = NextCost(u);
            if (cost < 0)
                return ($"'{u.Name}' is already at max level.", 0);
            if (credits < cost)
                return ($"Not enough credits for '{u.Name}': need {cost}, have {credits}.", 0);

            u.RuntimeLevel += 1;
            Persist(u);
            return ($"Upgraded '{u.Name}' to level {u.RuntimeLevel} ({u.Describe(u.RuntimeLevel)}). -{cost} credits.", cost);
        }

        public static string Reset()
        {
            foreach (var u in _upgrades)
            {
                u.RuntimeLevel = 0;
                Persist(u);
            }
            return "All AlliedDefenses upgrades reset to level 0.";
        }
    }
}

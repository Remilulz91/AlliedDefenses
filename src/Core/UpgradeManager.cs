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
    /// Levels are stored as BepInEx config values, so they PERSIST across everything —
    /// deaths, game overs, new save files — which is exactly what we want (no rage-quit
    /// reset of your progress). Reset is a deliberate "ally upgrade reset" command.
    ///
    /// The HOST owns the levels and drives all gameplay effects (damage, expiry), so no
    /// heavy network sync is needed for correctness. Buying deducts the ship's shared
    /// credits via the terminal.
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
            public ConfigEntry<int> Level = null!;
            public Func<int, string> Describe = _ => "";
        }

        private static readonly List<Upgrade> _upgrades = new();
        private static readonly Dictionary<string, Upgrade> _byId = new(StringComparer.OrdinalIgnoreCase);

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
                // Stored in the config file -> persists across sessions / game overs.
                Level = cfg.Bind("Upgrades (saved)", id + "Level", 0,
                    $"Current level of the '{name}' upgrade. Bought in-game with 'ally upgrade {id}'."),
            };
            _upgrades.Add(u);
            _byId[id] = u;
        }

        public static int LevelOf(string id) => _byId.TryGetValue(id, out var u) ? u.Level.Value : 0;

        /// <summary>Cost of the NEXT level, or -1 if maxed / unknown.</summary>
        public static int NextCost(Upgrade u)
        {
            if (u.Level.Value >= u.MaxLevel) return -1;
            return Mathf.RoundToInt(u.BaseCost * Mathf.Pow(u.CostGrowth, u.Level.Value));
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
            sb.AppendLine("");
            foreach (var u in _upgrades)
            {
                int cost = NextCost(u);
                string costText = cost < 0 ? "MAX" : $"{cost} cr";
                sb.AppendLine($"{u.Id,-12} Lv {u.Level.Value}/{u.MaxLevel}  ({u.Describe(u.Level.Value)})  next: {costText}");
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

            u.Level.Value += 1;
            return ($"Upgraded '{u.Name}' to level {u.Level.Value} ({u.Describe(u.Level.Value)}). -{cost} credits.", cost);
        }

        public static string Reset()
        {
            foreach (var u in _upgrades) u.Level.Value = 0;
            return "All AlliedDefenses upgrades reset to level 0.";
        }
    }
}

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

        /// <summary>Turret enemy-detection range (m) with the range upgrade applied.</summary>
        public static float EffectiveDetectionRange()
        {
            float baseRange = ModConfig.EnemyDetectionRange.Value;
            if (!ModConfig.EnableUpgrades.Value) return baseRange;
            return baseRange + LevelOf("turretrange") * 5f; // +5m per level
        }

        /// <summary>Allied-mine trigger radius (m) with the radius upgrade applied.</summary>
        public static float EffectiveMineRadius()
        {
            float baseRadius = ModConfig.MineTriggerRadius.Value;
            if (!ModConfig.EnableUpgrades.Value) return baseRadius;
            return baseRadius + LevelOf("mineradius"); // +1m per level
        }

        // --- Ghost Girl "sanity aura": allied defenses lower a nearby player's hidden
        //     insanity, which is what the Ghost Girl feeds on. Level 0 = off. ---
        public static bool SanityAuraEnabled => ModConfig.EnableUpgrades.Value && LevelOf("sanity") > 0;
        public static float SanityAuraRadius() => SanityRadiusFor(LevelOf("sanity"));
        public static float SanityAuraRate() => SanityRateFor(LevelOf("sanity"));
        private static float SanityRadiusFor(int lvl) => lvl <= 0 ? 0f : 6f + lvl * 2f;  // 8m..16m
        private static float SanityRateFor(int lvl) => lvl <= 0 ? 0f : lvl * 2f;          // 2..10 insanity/sec

        // --- Coil-Head neutralize: an allied turret watching a Coil-Head freezes it.
        //     Level 0 = off; each level extends how long the freeze lingers after the
        //     turret loses sight. ---
        public static bool NeutralizeEnabled => ModConfig.EnableUpgrades.Value && LevelOf("neutralize") > 0;
        public static float NeutralizeLinger() => NeutralizeLingerFor(LevelOf("neutralize"));
        private static float NeutralizeLingerFor(int lvl) => lvl <= 0 ? 0f : 0.5f + lvl * 0.5f; // 1.0s..3.0s

        // --- Earth Leviathan "seismic cloak": while near an allied defense (a placed beacon),
        //     the sand-worm can no longer target the player (EnemyAI.PlayerIsTargetable is
        //     forced false for SandWormAI). Level 0 = off; each level widens the radius. ---
        public static bool SeismicEnabled => ModConfig.EnableUpgrades.Value && LevelOf("seismic") > 0;
        public static float SeismicRadius() => SeismicRadiusFor(LevelOf("seismic"));
        private static float SeismicRadiusFor(int lvl) => lvl <= 0 ? 0f : 8f + lvl * 3f; // 11m..23m

        // --- Eyeless Dog "sound muffle": the (blind) dogs hunt purely by noise. While a noise
        //     is emitted inside an allied-defense radius, MouthDogAI.DetectNoise ignores it, so
        //     the player is effectively silent in the beacon's quiet zone. Level 0 = off. ---
        public static bool MuffleEnabled => ModConfig.EnableUpgrades.Value && LevelOf("muffle") > 0;
        public static float MuffleRadius() => MuffleRadiusFor(LevelOf("muffle"));
        private static float MuffleRadiusFor(int lvl) => lvl <= 0 ? 0f : 6f + lvl * 2f; // 8m..16m

        // --- Barber (ClaySurgeonAI) counter-play: the Barber "dances" toward the closest targetable
        //     player (TargetClosestPlayer -> PlayerIsTargetable). While near an allied defense the
        //     player is made untargetable, so the Barber won't jump toward them. Level 0 = off. ---
        public static bool BarberEnabled => ModConfig.EnableUpgrades.Value && LevelOf("barber") > 0;
        public static float BarberRadius() => BarberRadiusFor(LevelOf("barber"));
        private static float BarberRadiusFor(int lvl) => lvl <= 0 ? 0f : 6f + lvl * 2f; // 8m..16m

        // --- Hygrodere / slime (BlobAI) counter-play: it picks the closest targetable player via
        //     TargetClosestPlayer -> PlayerIsTargetable; if none is targetable it simply roams. So
        //     while near an allied defense the player is untargetable and the slow blob wanders off
        //     instead of following. Level 0 = off. ---
        public static bool SlimeEnabled => ModConfig.EnableUpgrades.Value && LevelOf("slime") > 0;
        public static float SlimeRadius() => SlimeRadiusFor(LevelOf("slime"));
        private static float SlimeRadiusFor(int lvl) => lvl <= 0 ? 0f : 6f + lvl * 2f; // 8m..16m

        // --- Circuit Bees (RedLocustBees) counter-play: their initial aggro is line-of-sight near
        //     the hive (can't be blocked), but their CHASE state validates the target through
        //     PlayerIsTargetable. So while near an allied defense the player is untargetable and the
        //     bees DROP the chase (game's own "lost target" path). Weaker than the others (disengage,
        //     not full stealth). Level 0 = off. ---
        public static bool BeesEnabled => ModConfig.EnableUpgrades.Value && LevelOf("bees") > 0;
        public static float BeesRadius() => BeesRadiusFor(LevelOf("bees"));
        private static float BeesRadiusFor(int lvl) => lvl <= 0 ? 0f : 6f + lvl * 2f; // 8m..16m

        /// <summary>Largest active aura radius (for the beacon's ground ring). 0 if none active.</summary>
        public static float MaxAuraRadius()
        {
            if (!ModConfig.EnableUpgrades.Value) return 0f;
            float r = 0f;
            r = Mathf.Max(r, SanityAuraRadius());
            r = Mathf.Max(r, SeismicRadius());
            r = Mathf.Max(r, MuffleRadius());
            r = Mathf.Max(r, BarberRadius());
            r = Mathf.Max(r, SlimeRadius());
            r = Mathf.Max(r, BeesRadius());
            return r;
        }

        // --- Defense Beacon ownership + "haul" (carry weight). The beacon is bought once via the
        //     'beacon' pseudo-upgrade (max level 1), so all the persistence / reset plumbing is
        //     shared. 'haul' lowers the beacon's weight so you carry it faster (floored so it is
        //     never weightless). ---
        public static bool BeaconOwned => LevelOf("beacon") > 0;
        public static float BeaconWeight() => BeaconWeightFor(LevelOf("haul"));
        private static float BeaconWeightFor(int lvl) => Mathf.Max(1.15f, 1.45f - lvl * 0.06f); // ~47lb -> ~16lb
        private static int WeightToLb(float w) => Mathf.RoundToInt((w - 1f) * 105f);

        // ----------------------------------------------------------------

        public static void Init(ConfigFile cfg)
        {
            _upgrades.Clear();
            _byId.Clear();

            Add(cfg, "duration", "Hijack duration", maxLevel: 10, baseCost: 120, growth: 1.5f,
                describe: lvl => $"{ModConfig.HijackDuration.Value + lvl * 20:0}s allied");

            Add(cfg, "turretdamage", "Turret damage", maxLevel: 15, baseCost: 100, growth: 1.4f,
                describe: lvl => $"{ModConfig.TurretEnemyDamage.Value + lvl} dmg/shot");

            Add(cfg, "turretrange", "Turret range", maxLevel: 8, baseCost: 100, growth: 1.4f,
                describe: lvl => $"{ModConfig.EnemyDetectionRange.Value + lvl * 5:0}m detect");

            Add(cfg, "mineradius", "Mine radius", maxLevel: 6, baseCost: 90, growth: 1.4f,
                describe: lvl => $"{ModConfig.MineTriggerRadius.Value + lvl:0}m radius");

            Add(cfg, "sanity", "Sanity aura", maxLevel: 5, baseCost: 150, growth: 1.5f,
                describe: lvl => lvl == 0 ? "off (Ghost Girl)" : $"{SanityRadiusFor(lvl):0}m, -{SanityRateFor(lvl):0}/s insanity");

            Add(cfg, "neutralize", "Neutralize", maxLevel: 5, baseCost: 200, growth: 1.5f,
                describe: lvl => lvl == 0 ? "off (Coil-Head)" : $"freeze, +{NeutralizeLingerFor(lvl):0.0}s linger");

            Add(cfg, "seismic", "Seismic cloak", maxLevel: 5, baseCost: 180, growth: 1.5f,
                describe: lvl => lvl == 0 ? "off (Earth Leviathan)" : $"{SeismicRadiusFor(lvl):0}m untargetable");

            Add(cfg, "muffle", "Sound muffle", maxLevel: 5, baseCost: 160, growth: 1.5f,
                describe: lvl => lvl == 0 ? "off (Eyeless Dog)" : $"{MuffleRadiusFor(lvl):0}m quiet zone");

            Add(cfg, "barber", "Barber cloak", maxLevel: 5, baseCost: 170, growth: 1.5f,
                describe: lvl => lvl == 0 ? "off (Barber)" : $"{BarberRadiusFor(lvl):0}m untargetable");

            Add(cfg, "slime", "Slime cloak", maxLevel: 5, baseCost: 140, growth: 1.5f,
                describe: lvl => lvl == 0 ? "off (Hygrodere)" : $"{SlimeRadiusFor(lvl):0}m untargetable");

            Add(cfg, "bees", "Bee cloak", maxLevel: 5, baseCost: 120, growth: 1.5f,
                describe: lvl => lvl == 0 ? "off (Circuit Bees)" : $"{BeesRadiusFor(lvl):0}m, bees disengage");

            if (ModConfig.EnableBeacon.Value)
            {
                // Bought once via 'ally beacon'; BaseCost mirrors the configurable price.
                Add(cfg, "beacon", "Defense Beacon", maxLevel: 1, baseCost: ModConfig.BeaconPrice.Value, growth: 1f,
                    describe: lvl => lvl == 0 ? "not owned" : "owned (carry with two hands)");

                Add(cfg, "haul", "Beacon haul", maxLevel: 5, baseCost: 90, growth: 1.4f,
                    describe: lvl => $"{WeightToLb(BeaconWeightFor(lvl))} lb carry");
            }
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

        /// <summary>
        /// Set an upgrade's in-effect level directly (no cost). Used by the network layer to mirror
        /// the team's upgrade levels onto every client. Clamped to the valid range. When
        /// <paramref name="persistOnHost"/> is true the host also saves it, so team upgrades bought
        /// by any player survive restarts (clients never persist the mirrored levels).
        /// </summary>
        public static void SetRuntimeLevel(string id, int level, bool persistOnHost = false)
        {
            if (!_byId.TryGetValue(id, out var u)) return;
            u.RuntimeLevel = Mathf.Clamp(level, 0, u.MaxLevel);
            if (persistOnHost) Persist(u);
        }

        /// <summary>Every upgrade's id and current level (for broadcasting the full set to a joiner).</summary>
        public static IEnumerable<(string id, int level)> AllRuntimeLevels()
        {
            foreach (var u in _upgrades)
                yield return (u.Id, u.RuntimeLevel);
        }

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

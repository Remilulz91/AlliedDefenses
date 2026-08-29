using BepInEx.Configuration;

namespace AlliedDefenses.Config
{
    /// <summary>
    /// Mod configuration, exposed in the file
    /// BepInEx/config/Remilulz_91.AlliedDefenses.cfg (created on first launch).
    ///
    /// Everything tunable lives here so there are no "magic numbers" scattered
    /// across the code. The in-game "config" terminal command reads these same
    /// values, so the players always see the real, current settings.
    /// </summary>
    /// <summary>Where the upgrade levels are stored (and therefore when they reset).</summary>
    public enum UpgradeSaveMode
    {
        /// <summary>Kept forever on this install; survive death AND game over. Reset only via 'ally upgrade reset'.</summary>
        Persistent,
        /// <summary>Tied to the current save slot: kept through deaths, but wiped on a game over (like the game's own progress).</summary>
        PerSave,
    }

    public static class ModConfig
    {
        /// <summary>Keyword typed in the terminal to hijack a defense.</summary>
        public static ConfigEntry<string> HijackCommand = null!;

        /// <summary>Seconds a defense stays allied. 0 = unlimited.</summary>
        public static ConfigEntry<float> HijackDuration = null!;

        /// <summary>Range (meters) at which an allied defense detects enemies.</summary>
        public static ConfigEntry<float> EnemyDetectionRange = null!;

        /// <summary>Radius (meters) at which an allied mine detonates on a nearby enemy.</summary>
        public static ConfigEntry<float> MineTriggerRadius = null!;

        /// <summary>If true, an allied defense never shoots a player (no friendly fire).</summary>
        public static ConfigEntry<bool> IgnorePlayersWhenAllied = null!;

        /// <summary>Credit cost to hijack a defense (0 = free).</summary>
        public static ConfigEntry<int> HijackCreditCost = null!;

        /// <summary>Base turret damage per shot to enemies (before upgrades).</summary>
        public static ConfigEntry<int> TurretEnemyDamage = null!;

        /// <summary>Enable the buy-with-credits upgrade system.</summary>
        public static ConfigEntry<bool> EnableUpgrades = null!;

        /// <summary>Enable the carryable Defense Beacon (bought once from the terminal).</summary>
        public static ConfigEntry<bool> EnableBeacon = null!;

        /// <summary>One-time credit cost of the Defense Beacon.</summary>
        public static ConfigEntry<int> BeaconPrice = null!;

        /// <summary>Keep the placed beacon standing upright (false = vanilla resting, may lie down).</summary>
        public static ConfigEntry<bool> BeaconUpright = null!;

        /// <summary>TEMPORARY testing aid: enables the 'ally givecredits &lt;n&gt;' command.</summary>
        public static ConfigEntry<bool> EnableDevCommands = null!;

        /// <summary>Whether upgrades are kept forever (Persistent) or reset on game over (PerSave).</summary>
        public static ConfigEntry<UpgradeSaveMode> UpgradePersistence = null!;

        // --- Visual feedback ---

        /// <summary>Tint allied defenses (laser, light, radar code) to show they're ours.</summary>
        public static ConfigEntry<bool> ColorAlliedDefenses = null!;

        /// <summary>In-world color for allied turrets' laser/light (HTML hex, e.g. 00FF00 = green).</summary>
        public static ConfigEntry<string> AlliedColorHex = null!;

        /// <summary>
        /// Radar-map color for allied defenses (HTML hex). Deliberately NOT green, since
        /// the game already uses green for "active" codes; blue avoids confusion.
        /// </summary>
        public static ConfigEntry<string> RadarAlliedColorHex = null!;

        public static void Init(ConfigFile cfg)
        {
            HijackCommand = cfg.Bind(
                "General", "HijackCommand", "ally",
                "Keyword typed in the terminal. In-game usage: <command> <id>  (e.g. ally A0)");

            HijackDuration = cfg.Bind(
                "General", "HijackDuration", 60f,
                "How many seconds a defense stays allied. Set to 0 for unlimited.");

            EnemyDetectionRange = cfg.Bind(
                "Targeting", "EnemyDetectionRange", 30f,
                "Maximum distance (m) at which a hijacked turret detects an enemy.");

            MineTriggerRadius = cfg.Bind(
                "Targeting", "MineTriggerRadius", 4f,
                "Radius (m) within which an allied mine detonates on a nearby enemy.");

            IgnorePlayersWhenAllied = cfg.Bind(
                "Targeting", "IgnorePlayersWhenAllied", true,
                "If true, an allied defense never fires at a player (recommended).");

            HijackCreditCost = cfg.Bind(
                "Economy", "HijackCreditCost", 0,
                "Credit cost to hijack a defense. 0 = free.");

            TurretEnemyDamage = cfg.Bind(
                "Economy", "TurretEnemyDamage", 1,
                "Base turret damage per shot to enemies (before upgrades).");

            EnableUpgrades = cfg.Bind(
                "Economy", "EnableUpgrades", true,
                "Enable the buy-with-credits upgrade system (ally upgrades / ally upgrade <id>).");

            UpgradePersistence = cfg.Bind(
                "Economy", "UpgradePersistence", UpgradeSaveMode.Persistent,
                "Persistent = upgrades are kept forever on this install (survive death AND game over). " +
                "PerSave = upgrades are tied to the current save slot: kept through deaths, but wiped on a game over.");

            EnableBeacon = cfg.Bind(
                "Beacon", "EnableBeacon", true,
                "Enable the carryable Defense Beacon (two-handed prop bought once with 'ally beacon'). " +
                "It anchors the protective auras (sanity/seismic/muffle) wherever you set it down.");

            BeaconPrice = cfg.Bind(
                "Beacon", "BeaconPrice", 175,
                "One-time credit cost of the Defense Beacon. Owned/paid state follows the same " +
                "Persistent/PerSave rule as the upgrades. Re-delivering a lost beacon is free.");

            BeaconUpright = cfg.Bind(
                "Beacon", "BeaconUpright", true,
                "Keep a placed beacon standing upright. Set to false to revert to the vanilla resting " +
                "behaviour (the model may lie on its side when dropped).");

            EnableDevCommands = cfg.Bind(
                "Dev", "EnableDevCommands", false,
                "TESTING ONLY. When true, enables 'ally givecredits <n>' to add ship credits so you " +
                "can test purchases without grinding. Leave false for normal play.");

            ColorAlliedDefenses = cfg.Bind(
                "Visuals", "ColorAlliedDefenses", true,
                "Tint allied defenses (turret laser/light and the radar code) so you can tell they're on your side.");

            AlliedColorHex = cfg.Bind(
                "Visuals", "AlliedColorHex", "00FF00",
                "In-world color for allied turret laser/light, HTML hex without '#'. Default 00FF00 (green).");

            RadarAlliedColorHex = cfg.Bind(
                "Visuals", "RadarAlliedColorHex", "1E90FF",
                "Radar-map color for allied defenses, HTML hex without '#'. Default 1E90FF (blue). " +
                "Avoid green here: the game already uses green for active codes.");
        }

        /// <summary>In-world allied color (laser/light). Falls back to green if invalid.</summary>
        public static UnityEngine.Color AlliedColor => ParseHex(AlliedColorHex.Value, UnityEngine.Color.green);

        /// <summary>Radar-map allied color. Falls back to blue if invalid.</summary>
        public static UnityEngine.Color RadarAlliedColor =>
            ParseHex(RadarAlliedColorHex.Value, new UnityEngine.Color(0.118f, 0.565f, 1f));

        private static UnityEngine.Color ParseHex(string hex, UnityEngine.Color fallback) =>
            UnityEngine.ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : fallback;
    }
}

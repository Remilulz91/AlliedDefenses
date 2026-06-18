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

        // --- Manual remote control ---

        /// <summary>Mouse look sensitivity while remote-controlling a turret.</summary>
        public static ConfigEntry<float> ManualControlSensitivity = null!;

        /// <summary>Key (Input System name) to release manual control. Default V.</summary>
        public static ConfigEntry<string> ManualControlReleaseKey = null!;

        /// <summary>Damage dealt per shot while a turret is manually controlled.</summary>
        public static ConfigEntry<int> ManualControlDamage = null!;

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

            ManualControlSensitivity = cfg.Bind(
                "Manual Control", "ManualControlSensitivity", 2f,
                "Mouse look sensitivity while remote-controlling a turret.");

            ManualControlReleaseKey = cfg.Bind(
                "Manual Control", "ManualControlReleaseKey", "V",
                "Key to release manual control (Unity Input System key name, e.g. V, B, Escape).");

            ManualControlDamage = cfg.Bind(
                "Manual Control", "ManualControlDamage", 50,
                "Damage dealt per shot while manually controlling a turret (vanilla turret = 50).");

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

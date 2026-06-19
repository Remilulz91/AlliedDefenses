using System.Text;
using AlliedDefenses.Config;
using AlliedDefenses.Core;

namespace AlliedDefenses.UI
{
    /// <summary>
    /// Builds the English text shown by the terminal commands. Kept here (not hard
    /// coded in the patch) so the wording is easy to find and edit, and so the
    /// "config" output always reflects the REAL current settings read from ModConfig.
    /// </summary>
    public static class CommandText
    {
        private static string Keyword => ModConfig.HijackCommand.Value.Trim();

        /// <summary>Short usage line (shown when the keyword is typed with no argument).</summary>
        public static string Usage()
        {
            string k = Keyword;
            return
                "ALLIED DEFENSES\n" +
                "-------------------------\n" +
                $"{k} <id>        : hijack one defense by its id (turret or mine, e.g. {k} U9)\n" +
                $"{k} turrets     : hijack every turret on the level\n" +
                $"{k} mines       : hijack every mine on the level\n" +
                $"{k} control     : control the NEAREST turret (no id needed)\n" +
                $"{k} control <id>: take manual remote control of a turret\n" +
                $"{k} release     : give back control of the turret you are driving\n" +
                $"{k} help        : how the mod works\n" +
                $"{k} config      : show the current settings\n";
        }

        /// <summary>Full explanation of how the mod works.</summary>
        public static string HowItWorks()
        {
            string k = Keyword;
            var sb = new StringBuilder();
            sb.AppendLine("ALLIED DEFENSES - HOW IT WORKS");
            sb.AppendLine("------------------------------------");
            sb.AppendLine("Normally the facility defenses target YOU (the employees) and");
            sb.AppendLine("ignore the monsters. This mod lets you flip that.");
            sb.AppendLine("");
            sb.AppendLine("TURRETS:");
            sb.AppendLine($"  {k} <id>   hijack one turret (same id you'd use to disable it).");
            sb.AppendLine($"  {k} turrets  hijack all turrets at once.");
            sb.AppendLine("  An allied turret stops shooting players and instead aims at");
            sb.AppendLine("  the nearest visible enemy in range.");
            sb.AppendLine("");
            sb.AppendLine("MINES:");
            sb.AppendLine($"  {k} <id>   hijack one mine (same id you'd use to disable it).");
            sb.AppendLine($"  {k} mines  hijack all mines at once.");
            sb.AppendLine("  An allied mine no longer explodes under players; it detonates");
            sb.AppendLine("  only when an enemy steps close to it.");
            sb.AppendLine("");
            sb.AppendLine("MANUAL CONTROL (turrets):");
            sb.AppendLine($"  {k} control        take over the NEAREST turret (no id needed).");
            sb.AppendLine($"  {k} control <id>   take over a specific turret.");
            sb.AppendLine("  The turret follows where you LOOK; LMB to fire (hits ANYTHING,");
            sb.AppendLine("  players included). Watch it turn and shoot in the world.");
            sb.AppendLine($"  Press the release key or '{k} release' to hand it back.");
            sb.AppendLine("");
            sb.AppendLine("All hijacks last for a set time, then the defense turns hostile");
            sb.AppendLine("again. Everyone in the lobby must have the mod; effects are synced.");
            sb.AppendLine("");
            sb.AppendLine($"Type  {k} config  to see the exact current settings.");
            return sb.ToString();
        }

        /// <summary>Current configuration, read live from the config values.</summary>
        public static string CurrentConfig()
        {
            float duration = ModConfig.HijackDuration.Value;
            string durationText = duration > 0f
                ? $"{duration:0} seconds, then it turns hostile again"
                : "unlimited (stays allied until end of round)";

            bool noFriendlyFire = ModConfig.IgnorePlayersWhenAllied.Value;
            int cost = ModConfig.HijackCreditCost.Value;
            string costText = cost > 0 ? $"{cost} credits per hijack" : "free";

            var sb = new StringBuilder();
            sb.AppendLine("ALLIED DEFENSES - CURRENT CONFIG");
            sb.AppendLine("------------------------------------");
            sb.AppendLine($"Command keyword       : {Keyword}");
            sb.AppendLine($"Allied duration       : {durationText}");
            sb.AppendLine($"Turret detect range   : {ModConfig.EnemyDetectionRange.Value:0} meters");
            sb.AppendLine($"Mine trigger radius   : {ModConfig.MineTriggerRadius.Value:0} meters");
            sb.AppendLine($"Players can be hit    : {(noFriendlyFire ? "NO - allied defenses never hurt players" : "yes - friendly fire is ON")}");
            sb.AppendLine($"Hijack cost           : {costText}");
            sb.AppendLine($"Allied colour cue     : {(ModConfig.ColorAlliedDefenses.Value ? "ON (green laser/light in dungeon, blue code on radar)" : "off")}");
            sb.AppendLine("");
            sb.AppendLine($"Defense types supported : {DefenseRegistry.Count}");
            sb.AppendLine($"Currently hijacked      : {HijackManager.ActiveCount}");
            sb.AppendLine("");
            sb.AppendLine("(Settings live in BepInEx/config/Remilulz_91.AlliedDefenses.cfg)");
            return sb.ToString();
        }
    }
}

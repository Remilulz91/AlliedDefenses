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
                $"{k} <id>     : hijack one defense by its id (turret, mine or spike, e.g. {k} U9)\n" +
                $"{k} turrets  : list all turrets and their ids\n" +
                $"{k} mines    : list all mines and their ids\n" +
                $"{k} spikes   : list all spike traps and their ids\n" +
                $"{k} upgrades : buy upgrades with ship credits\n" +
                $"{k} beacon   : buy/deliver the carryable Defense Beacon\n" +
                $"{k} help     : how the mod works\n" +
                $"{k} config   : show the current settings\n";
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
            sb.AppendLine($"  {k} turrets  list all turrets and their ids.");
            sb.AppendLine($"  {k} <id>   hijack one turret (same id you'd use to disable it).");
            sb.AppendLine("  An allied turret stops shooting players and instead aims at");
            sb.AppendLine("  the nearest visible enemy in range.");
            sb.AppendLine("");
            sb.AppendLine("MINES:");
            sb.AppendLine($"  {k} mines  list all mines and their ids.");
            sb.AppendLine($"  {k} <id>   hijack one mine (same id you'd use to disable it).");
            sb.AppendLine("  An allied mine no longer explodes under players; it detonates");
            sb.AppendLine("  only when an enemy steps close to it.");
            sb.AppendLine("");
            sb.AppendLine("SPIKE TRAPS:");
            sb.AppendLine($"  {k} spikes list all spike traps and their ids.");
            sb.AppendLine($"  {k} <id>   hijack one spike trap.");
            sb.AppendLine("  An allied spike trap no longer crushes players; it still");
            sb.AppendLine("  slams down on enemies caught underneath.");
            sb.AppendLine("");
            sb.AppendLine("UPGRADES:");
            sb.AppendLine($"  {k} upgrades       see upgrades, levels and costs.");
            sb.AppendLine($"  {k} upgrade <id>   buy the next level with ship credits.");
            sb.AppendLine($"  {k} upgrade reset  reset all upgrades to level 0.");
            sb.AppendLine("  Save mode is set in the config (UpgradePersistence):");
            sb.AppendLine("   - Persistent: kept forever, even through a game over (default).");
            sb.AppendLine("   - PerSave: tied to the save slot, wiped on a game over.");
            sb.AppendLine("");
            sb.AppendLine("DEFENSE BEACON (protection outside, near the ship):");
            sb.AppendLine($"  {k} beacon  buy it once, then it is delivered to the ship.");
            sb.AppendLine("  It is a heavy TWO-HANDED prop: carry it out (no loot while");
            sb.AppendLine("  carrying, you move slower) and set it down anywhere. Wherever");
            sb.AppendLine("  it sits it anchors the counter-play auras below, so they work");
            sb.AppendLine("  out in the field where there are no turrets or mines.");
            sb.AppendLine("  Bought once - re-delivered free if lost. 'haul' upgrade makes");
            sb.AppendLine("  it lighter to carry.");
            sb.AppendLine("");
            sb.AppendLine("UNKILLABLE ENEMIES (counter-play auras, near a defense OR beacon):");
            sb.AppendLine("  sanity     : calms your mind nearby, so the Ghost Girl targets");
            sb.AppendLine("               and escalates on you less.");
            sb.AppendLine("  neutralize : an allied turret watching a Coil-Head freezes it");
            sb.AppendLine("               in place (it can't be killed).");
            sb.AppendLine("  seismic    : the Earth Leviathan (sand worm) can't target you");
            sb.AppendLine("               while you stand in the radius (it hunts by proximity,");
            sb.AppendLine("               not sound, and is unkillable).");
            sb.AppendLine("  muffle     : Eyeless Dogs are blind and hunt by sound; noises");
            sb.AppendLine("               made in the radius are silenced, so they don't hear");
            sb.AppendLine("               you. Each is a separate upgrade (level 0 = off).");
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
            float duration = UpgradeManager.EffectiveDuration();
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
            sb.AppendLine($"Turret detect range   : {UpgradeManager.EffectiveDetectionRange():0} meters");
            sb.AppendLine($"Mine trigger radius   : {UpgradeManager.EffectiveMineRadius():0} meters");
            sb.AppendLine($"Players can be hit    : {(noFriendlyFire ? "NO - allied defenses never hurt players" : "yes - friendly fire is ON")}");
            sb.AppendLine($"Hijack cost           : {costText}");
            sb.AppendLine($"Allied colour cue     : {(ModConfig.ColorAlliedDefenses.Value ? "ON (green laser/light in dungeon, blue code on radar)" : "off")}");
            string beaconText = !ModConfig.EnableBeacon.Value ? "disabled"
                : UpgradeManager.BeaconOwned ? "owned" : $"available for {ModConfig.BeaconPrice.Value} credits";
            sb.AppendLine($"Defense Beacon        : {beaconText}");
            sb.AppendLine("");
            sb.AppendLine($"Defense types supported : {DefenseRegistry.Count}");
            sb.AppendLine($"Currently hijacked      : {HijackManager.ActiveCount}");
            sb.AppendLine("");
            sb.AppendLine("(Settings live in BepInEx/config/Remilulz_91.AlliedDefenses.cfg)");
            return sb.ToString();
        }
    }
}

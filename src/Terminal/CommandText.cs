using System.Text;
using AlliedDefenses.Config;
using AlliedDefenses.Core;

namespace AlliedDefenses.UI
{
    /// <summary>
    /// Builds the text shown by the terminal commands, in English or French (config: Language).
    /// Kept here (not hard-coded in the patch) so the wording is easy to find and edit, and so the
    /// "config" output always reflects the REAL current settings read from ModConfig.
    /// </summary>
    public static class CommandText
    {
        private static string Keyword => ModConfig.HijackCommand.Value.Trim();
        private static bool Fr => ModConfig.TerminalLanguage.Value == Language.Francais;

        public static string Usage() => Fr ? UsageFr() : UsageEn();
        public static string HowItWorks() => Fr ? HowItWorksFr() : HowItWorksEn();
        public static string CurrentConfig() => Fr ? CurrentConfigFr() : CurrentConfigEn();

        // ======================= ENGLISH =======================

        private static string UsageEn()
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
                $"{k} beacon   : buy the deployable Defense Beacon\n" +
                $"{k} hack     : buy the Hack Tool (aim at a door/turret + key)\n" +
                $"{k} help     : how the mod works\n" +
                $"{k} config   : show the current settings\n";
        }

        private static string HowItWorksEn()
        {
            string k = Keyword;
            var sb = new StringBuilder();
            sb.AppendLine("ALLIED DEFENSES - HOW IT WORKS");
            sb.AppendLine("------------------------------------");
            sb.AppendLine("Normally the facility defenses target YOU (the employees) and");
            sb.AppendLine("ignore the monsters. This mod lets you flip that.");
            sb.AppendLine("");
            sb.AppendLine("TURRETS / MINES / SPIKE TRAPS:");
            sb.AppendLine($"  {k} turrets | mines | spikes   list them and their ids.");
            sb.AppendLine($"  {k} <id>   hijack one (same id you'd use to disable it).");
            sb.AppendLine("  Allied, they hurt ENEMIES instead of players: turrets shoot the");
            sb.AppendLine("  nearest enemy, mines/spikes only trigger on enemies.");
            sb.AppendLine("");
            sb.AppendLine("UPGRADES:");
            sb.AppendLine($"  {k} upgrades       see upgrades, levels and costs.");
            sb.AppendLine($"  {k} upgrade <id>   buy the next level with ship credits.");
            sb.AppendLine($"  {k} upgrade reset  reset all upgrades to level 0.");
            sb.AppendLine("  Upgrades are TEAM-WIDE: bought once from shared credits, every");
            sb.AppendLine("  player in the lobby benefits (levels are synced to everyone).");
            sb.AppendLine("");
            sb.AppendLine("DEFENSE BEACON (deploy protection anywhere):");
            sb.AppendLine($"  {k} beacon  buy it once. Then press the deploy key ([{ModConfig.BeaconDeployKey.Value}] by");
            sb.AppendLine("  default) to drop it at your position - out in the field where there");
            sb.AppendLine("  are no turrets or mines. Press again to move it. It anchors the");
            sb.AppendLine("  counter-play auras below.");
            sb.AppendLine($"  Recall it with [{ModConfig.BeaconRecallKey.Value}] or '{k} beacon recall'.");
            sb.AppendLine("");
            sb.AppendLine("HACK TOOL (control the facility from inside):");
            sb.AppendLine($"  {k} hack  buy it. Then AIM at a locked big door, turret, mine or");
            sb.AppendLine($"  spike trap and press the hack key ([{ModConfig.HackKey.Value}]) to trigger it -");
            sb.AppendLine("  like typing its code at the terminal, but from inside the facility.");
            sb.AppendLine("");
            sb.AppendLine("UNKILLABLE ENEMIES (counter-play auras, near a defense OR beacon):");
            sb.AppendLine("  sanity     : calms your mind, so the Ghost Girl escalates less.");
            sb.AppendLine("  neutralize : an allied turret freezes a Coil-Head it watches.");
            sb.AppendLine("  seismic    : the sand worm can't target you in the radius.");
            sb.AppendLine("  muffle     : Eyeless Dogs (blind) can't hear noise in the radius.");
            sb.AppendLine("  barber     : the Barber can't target you in the radius.");
            sb.AppendLine("  slime      : the Hygrodere can't target you (it wanders off).");
            sb.AppendLine("  bees       : Circuit Bees drop their chase in the radius.");
            sb.AppendLine("  Each is a separate upgrade (level 0 = off).");
            sb.AppendLine("");
            sb.AppendLine("All hijacks last a set time, then turn hostile again. Everyone in");
            sb.AppendLine("the lobby must have the mod; effects are synced.");
            sb.AppendLine("");
            sb.AppendLine($"Type  {k} config  to see the exact current settings.");
            return sb.ToString();
        }

        private static string CurrentConfigEn()
        {
            var sb = new StringBuilder();
            sb.AppendLine("ALLIED DEFENSES - CURRENT CONFIG");
            sb.AppendLine("------------------------------------");
            sb.AppendLine($"Command keyword       : {Keyword}");
            sb.AppendLine($"Allied duration       : {DurationText(false)}");
            sb.AppendLine($"Turret detect range   : {UpgradeManager.EffectiveDetectionRange():0} meters");
            sb.AppendLine($"Mine trigger radius   : {UpgradeManager.EffectiveMineRadius():0} meters");
            sb.AppendLine($"Players can be hit    : {(ModConfig.IgnorePlayersWhenAllied.Value ? "NO - allied defenses never hurt players" : "yes - friendly fire is ON")}");
            sb.AppendLine($"Hijack cost           : {(ModConfig.HijackCreditCost.Value > 0 ? $"{ModConfig.HijackCreditCost.Value} credits per hijack" : "free")}");
            sb.AppendLine($"Allied colour cue     : {(ModConfig.ColorAlliedDefenses.Value ? "ON (green in dungeon, blue code on radar)" : "off")}");
            sb.AppendLine($"Defense Beacon        : {BeaconText(false)}");
            sb.AppendLine("");
            sb.AppendLine($"Defense types supported : {DefenseRegistry.Count}");
            sb.AppendLine($"Currently hijacked      : {HijackManager.ActiveCount}");
            sb.AppendLine("");
            sb.AppendLine("(Settings live in BepInEx/config/Remilulz_91.AlliedDefenses.cfg)");
            return sb.ToString();
        }

        // ======================= FRANCAIS =======================

        private static string UsageFr()
        {
            string k = Keyword;
            return
                "DEFENSES ALLIEES\n" +
                "-------------------------\n" +
                $"{k} <id>     : pirater une defense par son id (tourelle, mine ou pique, ex. {k} U9)\n" +
                $"{k} turrets  : lister toutes les tourelles et leurs ids\n" +
                $"{k} mines    : lister toutes les mines et leurs ids\n" +
                $"{k} spikes   : lister tous les pieges a pointes et leurs ids\n" +
                $"{k} upgrades : acheter des ameliorations avec les credits du vaisseau\n" +
                $"{k} beacon   : acheter la balise de defense deployable\n" +
                $"{k} hack     : acheter l'outil de piratage (viser + touche)\n" +
                $"{k} help     : comment fonctionne le mod\n" +
                $"{k} config   : afficher les reglages actuels\n";
        }

        private static string HowItWorksFr()
        {
            string k = Keyword;
            var sb = new StringBuilder();
            sb.AppendLine("DEFENSES ALLIEES - FONCTIONNEMENT");
            sb.AppendLine("------------------------------------");
            sb.AppendLine("Normalement les defenses du complexe VOUS visent (les employes)");
            sb.AppendLine("et ignorent les monstres. Ce mod inverse la situation.");
            sb.AppendLine("");
            sb.AppendLine("TOURELLES / MINES / PIEGES A POINTES :");
            sb.AppendLine($"  {k} turrets | mines | spikes   les lister avec leurs ids.");
            sb.AppendLine($"  {k} <id>   en pirater une (le meme id que pour la desactiver).");
            sb.AppendLine("  Alliees, elles frappent les ENNEMIS au lieu des joueurs : la");
            sb.AppendLine("  tourelle vise l'ennemi le plus proche, mines/pieges ne se");
            sb.AppendLine("  declenchent que sur les ennemis.");
            sb.AppendLine("");
            sb.AppendLine("AMELIORATIONS :");
            sb.AppendLine($"  {k} upgrades       voir les ameliorations, niveaux et couts.");
            sb.AppendLine($"  {k} upgrade <id>   acheter le niveau suivant avec les credits.");
            sb.AppendLine($"  {k} upgrade reset  remettre toutes les ameliorations a 0.");
            sb.AppendLine("  Les ameliorations sont D'EQUIPE : achetees une fois avec les");
            sb.AppendLine("  credits partages, tout le lobby en profite (niveaux synchronises).");
            sb.AppendLine("");
            sb.AppendLine("BALISE DE DEFENSE (protection deployable partout) :");
            sb.AppendLine($"  {k} beacon  achat unique. Ensuite appuie sur la touche de depot");
            sb.AppendLine($"  ([{ModConfig.BeaconDeployKey.Value}] par defaut) pour la poser a ta position, sur le terrain");
            sb.AppendLine("  la ou il n'y a ni tourelle ni mine. Reappuie pour la deplacer.");
            sb.AppendLine("  Elle ancre les auras de contre-jeu ci-dessous.");
            sb.AppendLine($"  Range-la avec [{ModConfig.BeaconRecallKey.Value}] ou '{k} beacon recall'.");
            sb.AppendLine("");
            sb.AppendLine("OUTIL DE PIRATAGE (piloter le complexe depuis l'interieur) :");
            sb.AppendLine($"  {k} hack  achete-le. Ensuite VISE une porte verrouillee, une");
            sb.AppendLine($"  tourelle, une mine ou un piege et appuie sur la touche ([{ModConfig.HackKey.Value}])");
            sb.AppendLine("  pour le declencher - comme taper son code, mais depuis l'interieur.");
            sb.AppendLine("");
            sb.AppendLine("ENNEMIS INCREVABLES (auras de contre-jeu, pres d'une defense/balise) :");
            sb.AppendLine("  sanity     : calme ton esprit, la Fille fantome t'escalade moins.");
            sb.AppendLine("  neutralize : une tourelle alliee fige un Coil-Head qu'elle regarde.");
            sb.AppendLine("  seismic    : le ver des sables ne peut pas te cibler dans le rayon.");
            sb.AppendLine("  muffle     : les Chiens (aveugles) n'entendent plus le bruit ici.");
            sb.AppendLine("  barber     : le Coiffeur ne peut pas te cibler dans le rayon.");
            sb.AppendLine("  slime      : l'Hygrodere ne peut pas te cibler (il s'en va).");
            sb.AppendLine("  bees       : les Abeilles abandonnent leur poursuite dans le rayon.");
            sb.AppendLine("  Chacune est une amelioration separee (niveau 0 = desactive).");
            sb.AppendLine("");
            sb.AppendLine("Chaque piratage dure un temps donne, puis redevient hostile. Tous");
            sb.AppendLine("les joueurs du lobby doivent avoir le mod ; les effets sont synchro.");
            sb.AppendLine("");
            sb.AppendLine($"Tape  {k} config  pour voir les reglages exacts.");
            return sb.ToString();
        }

        private static string CurrentConfigFr()
        {
            var sb = new StringBuilder();
            sb.AppendLine("DEFENSES ALLIEES - CONFIG ACTUELLE");
            sb.AppendLine("------------------------------------");
            sb.AppendLine($"Mot-cle de commande   : {Keyword}");
            sb.AppendLine($"Duree alliee          : {DurationText(true)}");
            sb.AppendLine($"Portee tourelle       : {UpgradeManager.EffectiveDetectionRange():0} metres");
            sb.AppendLine($"Rayon mine            : {UpgradeManager.EffectiveMineRadius():0} metres");
            sb.AppendLine($"Joueurs touchables    : {(ModConfig.IgnorePlayersWhenAllied.Value ? "NON - les defenses alliees n'atteignent jamais les joueurs" : "oui - tir ami ACTIF")}");
            sb.AppendLine($"Cout de piratage      : {(ModConfig.HijackCreditCost.Value > 0 ? $"{ModConfig.HijackCreditCost.Value} credits par piratage" : "gratuit")}");
            sb.AppendLine($"Indice couleur allie  : {(ModConfig.ColorAlliedDefenses.Value ? "OUI (vert dans le donjon, code bleu au radar)" : "non")}");
            sb.AppendLine($"Balise de defense     : {BeaconText(true)}");
            sb.AppendLine("");
            sb.AppendLine($"Types de defense pris en charge : {DefenseRegistry.Count}");
            sb.AppendLine($"Actuellement piratees           : {HijackManager.ActiveCount}");
            sb.AppendLine("");
            sb.AppendLine("(Reglages dans BepInEx/config/Remilulz_91.AlliedDefenses.cfg)");
            return sb.ToString();
        }

        // ======================= shared bits =======================

        private static string DurationText(bool fr)
        {
            float d = UpgradeManager.EffectiveDuration();
            if (d > 0f)
                return fr ? $"{d:0} secondes, puis redevient hostile" : $"{d:0} seconds, then it turns hostile again";
            return fr ? "illimitee (alliee jusqu'a la fin de la manche)" : "unlimited (stays allied until end of round)";
        }

        private static string BeaconText(bool fr)
        {
            if (!ModConfig.EnableBeacon.Value) return fr ? "desactivee" : "disabled";
            if (UpgradeManager.BeaconOwned) return fr ? "possedee" : "owned";
            return fr ? $"disponible pour {ModConfig.BeaconPrice.Value} credits"
                      : $"available for {ModConfig.BeaconPrice.Value} credits";
        }
    }
}

using System;
using AlliedDefenses.Config;
using AlliedDefenses.Core;
using AlliedDefenses.UI;
using HarmonyLib;
using UnityEngine;

namespace AlliedDefenses.Patches
{
    /// <summary>
    /// Intercepts what the player types in the ship computer to add our commands:
    ///
    ///   ally &lt;id&gt;    -> hijack a defense (e.g. ally A0)
    ///   ally help     -> explain how the mod works
    ///   ally config   -> show the current settings
    ///
    /// We hook Terminal.ParsePlayerSentence() (called when a line is submitted). If
    /// the line starts with the configured keyword we handle it and print our own
    /// response; otherwise the terminal answers as usual.
    /// </summary>
    [HarmonyPatch(typeof(Terminal))]
    public static class TerminalPatches
    {
        // PREFIX (not postfix): when the line is one of OUR commands, we fully replace
        // the terminal's response and SKIP the vanilla parser (return false). This
        // guarantees zero collision with the vanilla codes:
        //   - "U9" alone  -> not our keyword -> we return true -> vanilla runs normally
        //                    (the usual ~2s mine/turret disable).
        //   - "ally U9"   -> our keyword     -> we handle it and the vanilla parser
        //                    never sees the line, so it cannot also disable U9.
        [HarmonyPrefix]
        [HarmonyPatch("ParsePlayerSentence")]
        public static bool ParsePrefix(Terminal __instance, ref TerminalNode __result)
        {
            string typed = GetTypedText(__instance);
            if (string.IsNullOrWhiteSpace(typed)) return true; // let vanilla handle it

            typed = typed.Trim();
            string keyword = ModConfig.HijackCommand.Value.Trim();

            // The line must start with the keyword (e.g. "ally") to be ours.
            bool isKeywordOnly = typed.Equals(keyword, StringComparison.OrdinalIgnoreCase);
            bool startsWithKeyword = typed.StartsWith(keyword + " ", StringComparison.OrdinalIgnoreCase);
            if (!isKeywordOnly && !startsWithKeyword)
                return true; // not our command -> run the vanilla parser untouched

            string arg = isKeywordOnly ? "" : typed.Substring(keyword.Length).Trim();

            string message;
            if (string.IsNullOrEmpty(arg))
                message = CommandText.Usage();
            else if (arg.Equals("help", StringComparison.OrdinalIgnoreCase) ||
                     arg.Equals("howitworks", StringComparison.OrdinalIgnoreCase) ||
                     arg.Equals("info", StringComparison.OrdinalIgnoreCase))
                message = CommandText.HowItWorks();
            else if (arg.Equals("config", StringComparison.OrdinalIgnoreCase) ||
                     arg.Equals("settings", StringComparison.OrdinalIgnoreCase))
                message = CommandText.CurrentConfig();
            else if (arg.Equals("upgrades", StringComparison.OrdinalIgnoreCase))
                message = UpgradeManager.ListText(ShipCredits.Get(__instance));
            else if (arg.StartsWith("upgrade ", StringComparison.OrdinalIgnoreCase))
                message = HandleUpgrade(__instance, arg.Substring("upgrade ".Length).Trim());
            else if (arg.Equals("beacon", StringComparison.OrdinalIgnoreCase))
                message = HandleBeacon(__instance);
            else if (TryMatchGroup(arg, out string typeId))
                message = HijackManager.ListDefenses(typeId); // "ally mines" / "ally turrets" -> list ids
            else
                message = HijackManager.RequestHijack(arg); // treat the argument as a defense id

            __result = MakeNode(message);
            return false; // skip the vanilla parser entirely for our commands
        }

        /// <summary>Handles "ally upgrade &lt;id&gt;" and "ally upgrade reset".</summary>
        private static string HandleUpgrade(Terminal terminal, string sub)
        {
            if (sub.Equals("reset", StringComparison.OrdinalIgnoreCase))
            {
                string r = UpgradeManager.Reset();
                // Mirror the reset (all levels now 0) to the whole lobby.
                var net = Networking.HijackNetworker.Active;
                if (net != null)
                    foreach (var (id, level) in UpgradeManager.AllRuntimeLevels())
                        net.ShareUpgradeLevel(id, level);
                return r;
            }

            var net = Networking.HijackNetworker.Active;
            if (net != null && !net.IsServer)
            {
                // Client: only the host can change the shared credits, so ask the host to buy.
                net.RequestPurchase("upgrade", sub);
                return "Purchase request sent to the host (shared credits are host-side).";
            }

            int credits = ShipCredits.Get(terminal);
            var (msg, spent) = UpgradeManager.Buy(sub, credits);
            if (spent > 0)
            {
                ShipCredits.Set(terminal, credits - spent);
                net?.ShareUpgradeLevel(sub, UpgradeManager.LevelOf(sub));
            }
            return msg;
        }

        /// <summary>Handles "ally beacon": buy the beacon once, or re-deliver a missing one for free.</summary>
        private static string HandleBeacon(Terminal terminal)
        {
            var net = Networking.HijackNetworker.Active;
            if (net != null && !net.IsServer)
            {
                net.RequestPurchase("beacon", "");
                return "Beacon request sent to the host (shared credits are host-side).";
            }

            int credits = ShipCredits.Get(terminal);
            var (msg, spent) = Beacon.BeaconManager.Purchase(credits);
            if (spent > 0)
            {
                ShipCredits.Set(terminal, credits - spent);
                net?.ShareUpgradeLevel("beacon", UpgradeManager.LevelOf("beacon"));
            }
            return msg;
        }

        /// <summary>
        /// Returns true if the argument names a whole defense TYPE (e.g. "mines",
        /// "mine", "turrets", "turret") rather than a single defense id. This drives
        /// the group hijack. Works generically for any registered module.
        /// </summary>
        private static bool TryMatchGroup(string arg, out string typeId)
        {
            string a = arg.Trim().ToLowerInvariant();
            foreach (var module in DefenseRegistry.All)
            {
                string id = module.TypeId.ToLowerInvariant();          // "turret", "mine"
                string name = module.DisplayName.ToLowerInvariant();   // "turret", "mine"
                if (a == id || a == id + "s" || a == name || a == name + "s")
                {
                    typeId = module.TypeId;
                    return true;
                }
            }
            typeId = "";
            return false;
        }

        /// <summary>Reconstructs the text the player typed on the last line.</summary>
        private static string GetTypedText(Terminal terminal)
        {
            try
            {
                // screenText (TMP input) + textAdded (int): how many characters the
                // player just added. Standard trick to isolate their input.
                var screenText = Traverse.Create(terminal).Field("screenText").GetValue();
                string full = Traverse.Create(screenText).Property("text").GetValue<string>() ?? "";
                int added = Traverse.Create(terminal).Field("textAdded").GetValue<int>();
                if (added <= 0 || added > full.Length) return "";
                return full.Substring(full.Length - added);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Could not read terminal input: {e.Message}");
                return "";
            }
        }

        private static TerminalNode MakeNode(string text)
        {
            var node = ScriptableObject.CreateInstance<TerminalNode>();
            node.displayText = text + "\n\n";
            node.clearPreviousText = true;
            node.maxCharactersToType = 50;
            return node;
        }
    }
}

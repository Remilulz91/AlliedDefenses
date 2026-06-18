using System;
using HarmonyLib;
using UnityEngine;

namespace AlliedDefenses.Core
{
    /// <summary>
    /// Shared helper to find a game object from the code shown in the ship terminal
    /// (e.g. "A0", "U9", "H0"). In Lethal Company, every terminal-controllable
    /// object (turrets AND mines, plus big doors) carries a TerminalAccessibleObject
    /// component holding its code string.
    ///
    /// Both the turret and mine modules use this, so the lookup lives in one place.
    /// </summary>
    public static class TerminalCodeResolver
    {
        /// <summary>
        /// Find the component of type <paramref name="componentType"/> whose
        /// TerminalAccessibleObject code matches <paramref name="code"/>.
        /// Returns null if nothing matches.
        /// </summary>
        public static Component? Resolve(string code, Type componentType)
        {
            if (string.IsNullOrEmpty(code)) return null;

            foreach (var acc in UnityEngine.Object.FindObjectsOfType<TerminalAccessibleObject>())
            {
                string accCode = ReadObjectCode(acc);
                if (!string.Equals(accCode, code, StringComparison.OrdinalIgnoreCase)) continue;

                // The controllable object may sit on the same GameObject, a child or a parent.
                var comp = acc.GetComponentInChildren(componentType) ?? acc.GetComponentInParent(componentType);
                if (comp != null) return comp;
            }
            return null;
        }

        /// <summary>
        /// Defensive read of TerminalAccessibleObject's code string. Tries the known
        /// field name first, then a couple of fallbacks, so it keeps working if the
        /// field is renamed in a future game update.
        /// </summary>
        public static string ReadObjectCode(TerminalAccessibleObject acc)
        {
            foreach (var name in new[] { "objectCode", "codeString", "code" })
            {
                try
                {
                    var val = Traverse.Create(acc).Field(name).GetValue<string>();
                    if (!string.IsNullOrEmpty(val)) return val;
                }
                catch { /* try next */ }
            }
            return "";
        }
    }
}

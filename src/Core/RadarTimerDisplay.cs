using System.Collections.Generic;
using AlliedDefenses.Config;
using HarmonyLib;
using UnityEngine;

namespace AlliedDefenses.Core
{
    /// <summary>
    /// Shows the remaining hijack time RIGHT ON the radar map, glued to the green code
    /// box of each allied defense (the U9 / A0 label on the ship monitor and the
    /// terminal map view).
    ///
    /// How it works: the green code on the map is the TextMeshPro field
    /// "mapRadarText" carried by each TerminalAccessibleObject (the same component
    /// turrets and mines use for their terminal code). While a defense is allied we
    /// append a compact countdown under its code (e.g. "U9\n0:42"); when the hijack
    /// ends we restore the plain code. The map camera renders that text, so the timer
    /// appears next to the box automatically — no extra Canvas needed.
    ///
    /// The field is read defensively (reflection) so a future game rename only
    /// disables this cosmetic feature instead of breaking the mod.
    /// </summary>
    public static class RadarTimerDisplay
    {
        // Original code text per TerminalAccessibleObject (instanceID -> "U9").
        private static readonly Dictionary<int, string> _originalText = new();

        // Original text colour per TerminalAccessibleObject, so we can restore it.
        private static readonly Dictionary<int, Color> _originalColor = new();

        /// <summary>Called every frame for an active hijack to refresh the countdown.</summary>
        public static void Update(HijackEntry entry)
        {
            var tao = FindAccessible(entry.Defense);
            if (tao == null) return;

            var tmp = GetRadarText(tao);
            if (tmp == null) return;

            int id = tao.GetInstanceID();

            // Remember the plain code (and colour) the first time we touch this label.
            if (!_originalText.ContainsKey(id))
            {
                _originalText[id] = StripTimer(GetText(tmp));
                _originalColor[id] = GetColor(tmp);
            }

            string code = _originalText[id];

            string suffix;
            if (entry.ExpireTime <= 0f)
                suffix = "ALLY"; // unlimited duration -> no countdown
            else
                suffix = Format(Mathf.Max(0f, entry.ExpireTime - Time.time));

            SetText(tmp, code + "\n" + suffix);

            // Tint the radar code BLUE (configurable) so the ship operator can tell
            // which defenses are allied. We deliberately avoid green here because the
            // game already shows active codes in green.
            if (ModConfig.ColorAlliedDefenses.Value)
                SetColor(tmp, ModConfig.RadarAlliedColor);
        }

        /// <summary>Restore the plain code on the map when a defense stops being allied.</summary>
        public static void Restore(Component defense)
        {
            var tao = FindAccessible(defense);
            if (tao == null) return;

            int id = tao.GetInstanceID();
            var tmp = GetRadarText(tao);
            if (tmp != null)
            {
                if (_originalText.TryGetValue(id, out var code))
                    SetText(tmp, code);
                if (_originalColor.TryGetValue(id, out var color))
                    SetColor(tmp, color);
            }

            _originalText.Remove(id);
            _originalColor.Remove(id);
        }

        // ---- formatting ----
        private static string Format(float seconds)
        {
            int s = Mathf.CeilToInt(seconds);
            return $"{s / 60}:{s % 60:00}"; // m:ss, e.g. 0:42, 1:05
        }

        private static string StripTimer(string text)
        {
            if (string.IsNullOrEmpty(text)) return text ?? "";
            int nl = text.IndexOf('\n');
            return nl >= 0 ? text.Substring(0, nl) : text;
        }

        // ---- reflective access to TerminalAccessibleObject.mapRadarText (a TMP text) ----
        private static TerminalAccessibleObject? FindAccessible(Component defense)
        {
            if (defense == null) return null; // Unity-null: destroyed defense
            return defense.GetComponentInParent<TerminalAccessibleObject>()
                ?? defense.GetComponentInChildren<TerminalAccessibleObject>();
        }

        private static Component? GetRadarText(TerminalAccessibleObject tao)
        {
            foreach (var name in new[] { "mapRadarText", "radarText", "codeText" })
            {
                try
                {
                    var c = Traverse.Create(tao).Field(name).GetValue<Component>();
                    if (c != null) return c;
                }
                catch { /* try next */ }
            }
            return null;
        }

        private static string GetText(Component tmp)
        {
            try { return Traverse.Create(tmp).Property("text").GetValue<string>() ?? ""; }
            catch { return ""; }
        }

        private static void SetText(Component tmp, string value)
        {
            try { Traverse.Create(tmp).Property("text").SetValue(value); }
            catch { /* cosmetic only */ }
        }

        private static Color GetColor(Component tmp)
        {
            try { return Traverse.Create(tmp).Property("color").GetValue<Color>(); }
            catch { return Color.white; }
        }

        private static void SetColor(Component tmp, Color value)
        {
            try { Traverse.Create(tmp).Property("color").SetValue(value); }
            catch { /* cosmetic only */ }
        }
    }
}

using System.Collections.Generic;
using AlliedDefenses.Defenses;

namespace AlliedDefenses.Core
{
    /// <summary>
    /// Directory of hijackable-defense modules.
    ///
    /// This is the ONLY place to touch when plugging in a new weapon: add one line
    /// to RegisterDefaults() and you are done. The terminal, networking and manager
    /// will automatically use the new module.
    /// </summary>
    public static class DefenseRegistry
    {
        private static readonly List<IHijackableDefense> _defenses = new();

        public static IReadOnlyList<IHijackableDefense> All => _defenses;
        public static int Count => _defenses.Count;

        /// <summary>Modules shipped with the mod.</summary>
        public static void RegisterDefaults()
        {
            Register(new TurretHijack());
            Register(new MineHijack());
        }

        public static void Register(IHijackableDefense defense)
        {
            _defenses.Add(defense);
            Plugin.Log.LogInfo($"Defense module registered: {defense.TypeId} ({defense.DisplayName})");
        }

        /// <summary>
        /// Walk every module to find the defense matching a terminal code (e.g. "A0").
        /// The first module that recognizes the code wins.
        /// </summary>
        public static bool TryResolve(string code, out IHijackableDefense? module, out UnityEngine.Component? defense)
        {
            foreach (var d in _defenses)
            {
                if (d.TryResolveByTerminalCode(code, out var found) && found != null)
                {
                    module = d;
                    defense = found;
                    return true;
                }
            }

            module = null;
            defense = null;
            return false;
        }

        /// <summary>Find a module by its type id ("turret", "mine"...).</summary>
        public static IHijackableDefense? FindModule(string typeId)
        {
            foreach (var d in _defenses)
                if (d.TypeId == typeId) return d;
            return null;
        }
    }
}

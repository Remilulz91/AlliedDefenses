using System.Collections.Generic;
using AlliedDefenses.Config;
using UnityEngine;

namespace AlliedDefenses.Core
{
    /// <summary>
    /// Generic in-world light tint: turns every Light under a defense to the allied
    /// colour while hijacked, and restores the originals afterwards. Used by the mine
    /// (whose only obvious cue otherwise is the radar code) so you can tell at a glance
    /// it is on your side. Robust component scan, no fragile field names.
    ///
    /// Apply(comp, true) can be called every frame (it re-asserts green over anything
    /// the game animates back to red); the original colours are captured once.
    /// </summary>
    public static class AlliedLightTint
    {
        private sealed class Cache
        {
            public Light[] Lights = System.Array.Empty<Light>();
            public Color[] Colors = System.Array.Empty<Color>();
        }

        private static readonly Dictionary<int, Cache> _cache = new();

        public static void Apply(Component defense, bool allied)
        {
            if (!ModConfig.ColorAlliedDefenses.Value) return;
            int id = defense.GetInstanceID();

            if (allied)
            {
                if (!_cache.TryGetValue(id, out var cache))
                {
                    var lights = defense.GetComponentsInChildren<Light>(true);
                    cache = new Cache { Lights = lights, Colors = new Color[lights.Length] };
                    for (int i = 0; i < lights.Length; i++) cache.Colors[i] = lights[i].color;
                    _cache[id] = cache;
                }

                Color c = ModConfig.AlliedColor;
                foreach (var l in cache.Lights)
                    if (l != null) l.color = c;
            }
            else if (_cache.TryGetValue(id, out var cache))
            {
                for (int i = 0; i < cache.Lights.Length; i++)
                    if (cache.Lights[i] != null) cache.Lights[i].color = cache.Colors[i];
                _cache.Remove(id);
            }
        }
    }
}

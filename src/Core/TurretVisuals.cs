using System.Collections.Generic;
using AlliedDefenses.Config;
using UnityEngine;

namespace AlliedDefenses.Core
{
    /// <summary>
    /// In-world colour feedback for allied turrets: tints the turret's laser beam and
    /// its light(s) to the allied colour (green by default), so anyone in the dungeon
    /// can instantly tell a turret is on their side. The original colours are restored
    /// when the turret turns hostile again.
    ///
    /// We grab the components with GetComponentsInChildren&lt;LineRenderer&gt;() and
    /// &lt;Light&gt;() rather than relying on exact field names, so this keeps working
    /// across game updates. The light is always on while the turret is powered, so it's
    /// the dependable "allied or not" cue; the laser tint applies whenever the beam is
    /// actually being drawn.
    /// </summary>
    public static class TurretVisuals
    {
        private sealed class Cache
        {
            public LineRenderer[] Lines = System.Array.Empty<LineRenderer>();
            public Color[] LineStart = System.Array.Empty<Color>();
            public Color[] LineEnd = System.Array.Empty<Color>();
            public Light[] Lights = System.Array.Empty<Light>();
            public Color[] LightColors = System.Array.Empty<Color>();
        }

        private static readonly Dictionary<int, Cache> _cache = new();

        // Our own dedicated beam LineRenderer per turret, so we never fight the vanilla one.
        private static readonly Dictionary<int, LineRenderer> _beams = new();

        public static void SetAllied(Turret turret, bool allied)
        {
            if (!ModConfig.ColorAlliedDefenses.Value) return;
            int id = turret.GetInstanceID();

            if (allied)
            {
                if (!_cache.ContainsKey(id))
                    _cache[id] = Capture(turret);

                Color c = ModConfig.AlliedColor;
                var cache = _cache[id];

                foreach (var lr in cache.Lines)
                {
                    if (lr == null) continue;
                    lr.startColor = c;
                    lr.endColor = c;
                }
                foreach (var light in cache.Lights)
                {
                    if (light == null) continue;
                    light.color = c;
                }
            }
            else
            {
                if (!_cache.TryGetValue(id, out var cache)) return;

                for (int i = 0; i < cache.Lines.Length; i++)
                {
                    if (cache.Lines[i] == null) continue;
                    cache.Lines[i].startColor = cache.LineStart[i];
                    cache.Lines[i].endColor = cache.LineEnd[i];
                }
                for (int i = 0; i < cache.Lights.Length; i++)
                {
                    if (cache.Lights[i] == null) continue;
                    cache.Lights[i].color = cache.LightColors[i];
                }
                _cache.Remove(id);

                // Remove our dedicated allied beam.
                if (_beams.TryGetValue(id, out var beam))
                {
                    if (beam != null) Object.Destroy(beam.gameObject);
                    _beams.Remove(id);
                }
            }
        }

        /// <summary>
        /// Draw the permanent allied beam from the turret barrel to 'to' (world space),
        /// in the in-world allied colour (green). Created lazily; reuses the game's own
        /// LineRenderer material so it renders correctly under HDRP.
        /// </summary>
        public static void DrawBeam(Turret turret, Vector3 from, Vector3 to)
        {
            if (!ModConfig.ColorAlliedDefenses.Value) return;

            int id = turret.GetInstanceID();
            if (!_beams.TryGetValue(id, out var lr) || lr == null)
            {
                lr = CreateBeam(turret);
                _beams[id] = lr;
            }

            lr.enabled = true;
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
        }

        /// <summary>Hide the allied beam (e.g. when the turret has no current target).</summary>
        public static void HideBeam(Turret turret)
        {
            if (_beams.TryGetValue(turret.GetInstanceID(), out var lr) && lr != null)
                lr.enabled = false;
        }

        private static LineRenderer CreateBeam(Turret turret)
        {
            var go = new GameObject("AlliedDefenses_Beam");
            go.transform.SetParent(turret.transform, false);

            var lr = go.AddComponent<LineRenderer>();

            // Reuse a working material from the turret's existing laser so HDRP renders it.
            var existing = turret.GetComponentInChildren<LineRenderer>(true);
            if (existing != null && existing.sharedMaterial != null)
                lr.material = new Material(existing.sharedMaterial);

            Color c = ModConfig.AlliedColor; // green
            lr.startColor = c;
            lr.endColor = c;
            try { if (lr.material != null) lr.material.color = c; } catch { /* some shaders lack _Color */ }

            lr.startWidth = existing != null ? existing.startWidth : 0.04f;
            lr.endWidth = existing != null ? existing.endWidth : 0.04f;
            lr.numCapVertices = 2;
            lr.enabled = false;
            return lr;
        }

        private static Cache Capture(Turret turret)
        {
            var lines = turret.GetComponentsInChildren<LineRenderer>(true);
            var lights = turret.GetComponentsInChildren<Light>(true);

            var cache = new Cache
            {
                Lines = lines,
                LineStart = new Color[lines.Length],
                LineEnd = new Color[lines.Length],
                Lights = lights,
                LightColors = new Color[lights.Length]
            };

            for (int i = 0; i < lines.Length; i++)
            {
                cache.LineStart[i] = lines[i].startColor;
                cache.LineEnd[i] = lines[i].endColor;
            }
            for (int i = 0; i < lights.Length; i++)
                cache.LightColors[i] = lights[i].color;

            return cache;
        }
    }
}

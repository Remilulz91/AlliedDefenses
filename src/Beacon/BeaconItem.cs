using HarmonyLib;
using UnityEngine;
using AlliedDefenses.Config;
using AlliedDefenses.Core;

namespace AlliedDefenses.Beacon
{
    /// <summary>
    /// The carryable "Defense Beacon": a two-handed heavy prop (a <see cref="GrabbableObject"/>)
    /// you buy once, haul out of the ship, and set down anywhere. Wherever it is — held or on the
    /// ground — it registers its position in <see cref="BeaconRegistry"/>, the anchor the protective
    /// auras key off (Ghost Girl sanity drain, sand-worm untargetable, Eyeless-Dog muffle).
    ///
    /// It is inert on purpose (it hides/deters, it does not shoot). At runtime it borrows a vanilla
    /// item's model + inventory icon (BeaconVisuals) and draws a green ground ring showing the
    /// current aura radius.
    /// </summary>
    public class BeaconItem : GrabbableObject
    {
        private bool _registered;
        private bool _lookApplied;
        private Light _light;
        private LineRenderer _ring;
        private LineRenderer _mapRing; // second ring, on the radar-map layer, shown on the ship monitor
        private bool _mapRingFailed;
        private float _ringTimer;

        private const float PivotHeightAboveFloor = 0.5f; // matches verticalOffset / spawn lift
        private const int RingSegments = 48;

        public override void Start()
        {
            base.Start();
            Register();
            _light = GetComponentInChildren<Light>();

            if (!_lookApplied)
            {
                BeaconVisuals.ApplyVanillaLook(this);
                _lookApplied = true;
            }
            SetupRing();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            // Diagnostic: proves the beacon replicated to this peer. If a CLIENT never logs this
            // (but the host does), the prefab isn't spawning on the client (hash/registration).
            Plugin.Log.LogInfo($"BeaconItem: OnNetworkSpawn (IsServer={IsServer}, IsClient={IsClient}).");
        }

        private void OnEnable() => Register();

        private void Register()
        {
            if (_registered) return;
            BeaconRegistry.Register(transform);
            _registered = true;
        }

        public override void Update()
        {
            base.Update();

            // Glow only when placed: kill the light while carried so it doesn't blind the holder.
            if (_light != null && _light.enabled == isHeld)
                _light.enabled = !isHeld;

            // Refresh the ground ring a few times a second.
            _ringTimer += Time.deltaTime;
            if (_ringTimer >= 0.4f)
            {
                _ringTimer = 0f;
                UpdateRing();
            }
        }

        public override void OnDestroy()
        {
            BeaconRegistry.Unregister(transform);
            _registered = false;
            base.OnDestroy();
        }

        // ---- ground ring showing the aura radius ----

        private void SetupRing()
        {
            if (_ring != null) return;
            try
            {
                var go = new GameObject("BeaconRing");
                go.transform.SetParent(transform, false);
                _ring = go.AddComponent<LineRenderer>();
                _ring.useWorldSpace = true; // world space so the ring stays flat regardless of beacon rotation
                _ring.loop = true;
                _ring.widthMultiplier = 0.08f;
                _ring.positionCount = RingSegments;
                _ring.numCapVertices = 2;
                _ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _ring.receiveShadows = false;

                var color = ModConfig.BeaconRingColor;
                var sh = Shader.Find("HDRP/Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
                if (sh != null)
                {
                    var mat = new Material(sh);
                    TrySetColor(mat, color, "_UnlitColor", "_BaseColor", "_Color");
                    TrySetColor(mat, color * 4f, "_EmissiveColor");
                    _ring.material = mat;
                }
                _ring.startColor = _ring.endColor = color;
                _ring.enabled = false;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"BeaconItem: could not build the ring ({e.Message}).");
            }
        }

        private void UpdateRing()
        {
            if (_ring == null) return;
            EnsureMapRing();

            float r = UpgradeManager.MaxAuraRadius();
            bool show = !isHeld && r > 0.1f;
            _ring.enabled = show;
            if (_mapRing != null) _mapRing.enabled = show;
            if (!show) return;

            // World-space horizontal circle centred under the beacon, just above the floor. Both the
            // in-world ring and the monitor ring share the same points.
            Vector3 c = transform.position;
            float ringY = c.y - PivotHeightAboveFloor + 0.05f;
            for (int i = 0; i < RingSegments; i++)
            {
                float a = (i / (float)RingSegments) * Mathf.PI * 2f;
                var p = new Vector3(c.x + Mathf.Cos(a) * r, ringY, c.z + Mathf.Sin(a) * r);
                _ring.SetPosition(i, p);
                if (_mapRing != null) _mapRing.SetPosition(i, p);
            }
        }

        /// <summary>
        /// Lazily build a second ring that renders on the ship monitor. We copy the layer and
        /// material from the game's own radar map line (lineFromRadarTargetToExit / radarLineMaterial
        /// on StartOfRound.mapScreen), which is proof a LineRenderer shows on the map camera. Only
        /// built once the map screen exists; guarded so a failure just skips the monitor ring.
        /// </summary>
        private void EnsureMapRing()
        {
            if (_mapRing != null || _mapRingFailed) return;
            if (!ModConfig.BeaconRingOnMonitor.Value) return;

            var sor = StartOfRound.Instance;
            var mapScreen = sor != null ? sor.mapScreen : null;
            if (mapScreen == null) return; // not ready yet; try again next tick

            try
            {
                var exitLine = Traverse.Create(mapScreen).Field("lineFromRadarTargetToExit").GetValue<LineRenderer>();
                var mapMat = Traverse.Create(mapScreen).Field("radarLineMaterial").GetValue<Material>();
                int mapLayer = exitLine != null ? exitLine.gameObject.layer : gameObject.layer;

                var go = new GameObject("BeaconMapRing");
                go.transform.SetParent(transform, false);
                go.layer = mapLayer;
                _mapRing = go.AddComponent<LineRenderer>();
                _mapRing.useWorldSpace = true;
                _mapRing.loop = true;
                _mapRing.widthMultiplier = 0.6f; // the map is zoomed out, so a thin line is invisible
                _mapRing.positionCount = RingSegments;
                _mapRing.numCapVertices = 2;
                _mapRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _mapRing.receiveShadows = false;

                // Copy the radar line material (so it renders on the map camera) but recolour the
                // INSTANCE — never the shared material, or we'd repaint the game's own exit line.
                var color = ModConfig.BeaconRingColor;
                if (mapMat != null)
                {
                    var inst = new Material(mapMat);
                    TrySetColor(inst, color, "_UnlitColor", "_BaseColor", "_Color", "_EmissiveColor");
                    _mapRing.material = inst;
                }
                _mapRing.startColor = _mapRing.endColor = color;
                _mapRing.enabled = false;
            }
            catch (System.Exception e)
            {
                _mapRingFailed = true;
                Plugin.Log.LogWarning($"BeaconItem: could not build the monitor ring ({e.Message}).");
            }
        }

        private static void TrySetColor(Material mat, Color c, params string[] props)
        {
            foreach (var p in props)
                if (mat.HasProperty(p)) { mat.SetColor(p, c); return; }
        }
    }
}

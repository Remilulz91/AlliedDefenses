using HarmonyLib;
using UnityEngine;
using AlliedDefenses.Config;
using AlliedDefenses.Core;

namespace AlliedDefenses.Beacon
{
    /// <summary>
    /// The deployed Defense Beacon — a PLAIN MonoBehaviour (NOT a GrabbableObject / NetworkBehaviour).
    /// It sits on a GameObject that only carries a NetworkObject, which the host spawns at a position.
    /// A bare NetworkObject replicates its existence + spawn transform reliably to every client, and
    /// this MonoBehaviour's Start runs on each peer to build the look, the ground ring, and register
    /// the position in <see cref="BeaconRegistry"/> (the anchor the counter-play auras key off).
    ///
    /// No grab, no reparenting, no per-frame network state -> none of the fragile custom-grabbable
    /// networking that crashed clients. To move it, the host despawns and re-spawns it elsewhere.
    /// </summary>
    public class BeaconObject : MonoBehaviour
    {
        private bool _registered;
        private bool _lookApplied;
        private Light _light;
        private LineRenderer _ring;
        private LineRenderer _mapRing;
        private bool _mapRingFailed;
        private float _ringTimer;

        private const float PivotHeightAboveFloor = 0.5f;
        private const int RingSegments = 48;

        private void Start()
        {
            Register();
            _light = GetComponentInChildren<Light>();
            if (!_lookApplied) { BeaconVisuals.ApplyVanillaLook(transform); _lookApplied = true; }
            SetupRing();
            UpdateRing();
        }

        private void OnEnable() => Register();

        private void Register()
        {
            if (_registered) return;
            BeaconRegistry.Register(transform);
            _registered = true;
        }

        private void OnDestroy()
        {
            BeaconRegistry.Unregister(transform);
            _registered = false;
        }

        private void Update()
        {
            _ringTimer += Time.deltaTime;
            if (_ringTimer >= 0.4f) { _ringTimer = 0f; UpdateRing(); }
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
                _ring.useWorldSpace = true;
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
                Plugin.Log.LogWarning($"BeaconObject: could not build the ring ({e.Message}).");
            }
        }

        private void UpdateRing()
        {
            if (_ring == null) return;
            EnsureMapRing();

            float r = UpgradeManager.MaxAuraRadius();
            bool show = r > 0.1f;
            _ring.enabled = show;
            if (_mapRing != null) _mapRing.enabled = show;
            if (!show) return;

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

        private void EnsureMapRing()
        {
            if (_mapRing != null || _mapRingFailed) return;
            if (!ModConfig.BeaconRingOnMonitor.Value) return;

            var sor = StartOfRound.Instance;
            var mapScreen = sor != null ? sor.mapScreen : null;
            if (mapScreen == null) return;

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
                _mapRing.widthMultiplier = 0.6f;
                _mapRing.positionCount = RingSegments;
                _mapRing.numCapVertices = 2;
                _mapRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _mapRing.receiveShadows = false;

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
                Plugin.Log.LogWarning($"BeaconObject: could not build the monitor ring ({e.Message}).");
            }
        }

        private static void TrySetColor(Material mat, Color c, params string[] props)
        {
            foreach (var p in props)
                if (mat.HasProperty(p)) { mat.SetColor(p, c); return; }
        }
    }
}

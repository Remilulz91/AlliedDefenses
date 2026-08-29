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

            // Keep a placed beacon standing upright (the donor model otherwise lies on its side when
            // dropped). We only flatten pitch/roll and keep the yaw, so it can still face any way.
            // Config-toggle so it's a clean rollback to the vanilla resting look.
            if (!isHeld && ModConfig.BeaconUpright.Value)
            {
                Vector3 e = transform.eulerAngles;
                if (Mathf.Abs(Mathf.DeltaAngle(e.x, 0f)) > 0.5f || Mathf.Abs(Mathf.DeltaAngle(e.z, 0f)) > 0.5f)
                    transform.rotation = Quaternion.Euler(0f, e.y, 0f);
            }

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

                var green = ModConfig.AlliedColor;
                var sh = Shader.Find("HDRP/Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
                if (sh != null)
                {
                    var mat = new Material(sh);
                    TrySetColor(mat, green, "_UnlitColor", "_BaseColor", "_Color");
                    TrySetColor(mat, green * 4f, "_EmissiveColor");
                    _ring.material = mat;
                }
                _ring.startColor = _ring.endColor = green;
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
            float r = UpgradeManager.MaxAuraRadius();
            bool show = !isHeld && r > 0.1f;
            _ring.enabled = show;
            if (!show) return;

            // World-space horizontal circle centred under the beacon, just above the floor.
            Vector3 c = transform.position;
            float ringY = c.y - PivotHeightAboveFloor + 0.05f;
            for (int i = 0; i < RingSegments; i++)
            {
                float a = (i / (float)RingSegments) * Mathf.PI * 2f;
                _ring.SetPosition(i, new Vector3(c.x + Mathf.Cos(a) * r, ringY, c.z + Mathf.Sin(a) * r));
            }
        }

        private static void TrySetColor(Material mat, Color c, params string[] props)
        {
            foreach (var p in props)
                if (mat.HasProperty(p)) { mat.SetColor(p, c); return; }
        }
    }
}

using HarmonyLib;
using UnityEngine;

namespace AlliedDefenses.Monitor
{
    /// <summary>
    /// Turns the ship monitor into the controlled turret's gun-cam while you drive it,
    /// then restores the normal radar feed when you release.
    ///
    /// HOW IT WORKS (and what to expect):
    /// The ship monitor is driven by a ManualCameraRenderer (StartOfRound.Instance.mapScreen)
    /// whose camera renders into a RenderTexture shown on the monitor mesh. The proven
    /// technique (used by OpenBodyCams / Helmet Cameras) is to render a SECOND camera into
    /// that same RenderTexture. Here we create a camera on the turret barrel, point the
    /// monitor's RenderTexture at it, and disable the radar camera for the duration.
    ///
    /// ⚠️ THIS IS THE PART THAT NEEDS IN-ENGINE TUNING. Camera culling masks, near/far
    /// planes, fog/volumetrics, lighting layers and performance all need to be checked
    /// live in the game; getting a clean image usually takes a few iterations. The logic
    /// below is a working starting point, not a guaranteed-perfect render. If you want a
    /// production-grade feed, OpenBodyCams is the reference implementation to study.
    ///
    /// This runs locally on the controlling player's client only, so only their monitor
    /// switches to the gun-cam; other players keep seeing the radar.
    /// </summary>
    public static class TurretMonitorFeed
    {
        private static GameObject? _camObject;
        private static Camera? _turretCam;
        private static Camera? _radarCam;        // the camera we temporarily disable
        private static bool _active;

        public static void Show(ulong turretNetId)
        {
            if (_active) Hide();

            var radarCam = GetRadarCamera();
            if (radarCam == null || radarCam.targetTexture == null)
            {
                Plugin.Log.LogWarning("TurretMonitorFeed: could not find the ship monitor camera; gun-cam disabled.");
                return;
            }

            if (!ResolveTurret(turretNetId, out var rod, out var pivot, out var muzzle)) return;

            if (_camObject == null)
            {
                _camObject = new GameObject("AlliedDefenses_TurretCam");
                Object.DontDestroyOnLoad(_camObject);
                _turretCam = _camObject.AddComponent<Camera>();
            }

            // Match the radar camera's output so the monitor shows our feed.
            _turretCam!.targetTexture = radarCam.targetTexture;
            _turretCam.cullingMask = radarCam.cullingMask; // TODO: tune layers for a turret POV
            _turretCam.nearClipPlane = 0.3f;
            _turretCam.farClipPlane = 100f;
            _turretCam.fieldOfView = 65f;

            // Parent to the ROTATING rod so the camera follows the aim, sit at the muzzle
            // and look along the barrel direction (pivot -> muzzle).
            _camObject.transform.SetParent(rod, true);
            _camObject.transform.position = muzzle.position - (muzzle.position - pivot.position).normalized * 0.4f;
            _camObject.transform.rotation = Quaternion.LookRotation((muzzle.position - pivot.position).normalized);

            _turretCam.enabled = true;
            _radarCam = radarCam;
            _radarCam.enabled = false; // hand the monitor over to the gun-cam
            _active = true;
        }

        public static void Hide()
        {
            if (!_active) return;

            if (_turretCam != null) _turretCam.enabled = false;
            if (_camObject != null) _camObject.transform.SetParent(null);
            if (_radarCam != null) _radarCam.enabled = true; // restore the radar feed

            _radarCam = null;
            _active = false;
        }

        // The radar/map camera that renders the ship monitor. ManualCameraRenderer
        // exposes it; we read it defensively (field name "cam", fallback "mapCamera").
        private static Camera? GetRadarCamera()
        {
            var screen = StartOfRound.Instance != null ? StartOfRound.Instance.mapScreen : null;
            if (screen == null) return null;

            foreach (var name in new[] { "cam", "mapCamera" })
            {
                try
                {
                    var c = Traverse.Create(screen).Field(name).GetValue<Camera>();
                    if (c != null) return c;
                }
                catch { /* try next */ }
            }
            return null;
        }

        private static bool ResolveTurret(ulong netId, out Transform rod, out Transform pivot, out Transform muzzle)
        {
            rod = pivot = muzzle = null!;
            var sm = Unity.Netcode.NetworkManager.Singleton?.SpawnManager;
            if (sm == null || !sm.SpawnedObjects.TryGetValue(netId, out var no) || no == null) return false;

            var turret = no.GetComponentInChildren<Turret>();
            if (turret == null) return false;

            rod = turret.turretRod;
            pivot = turret.centerPoint;
            muzzle = turret.aimPoint;
            return rod != null && pivot != null && muzzle != null;
        }
    }
}

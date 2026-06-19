using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace AlliedDefenses.Monitor
{
    /// <summary>
    /// Remote view for manual turret control, built on top of OpenBodyCams (OBC).
    ///
    /// Instead of rendering our own camera (hard), we reuse OBC's MAIN body cam (the
    /// bottom-right ship monitor) and simply RETARGET it to the turret's muzzle while
    /// you drive it, then restore it to the local player on release. OBC does all the
    /// heavy rendering, including auto-exposure (which brightens dark areas).
    ///
    /// We do NOT add a light: the body cam's HDRP auto-exposure blows any added light out
    /// to pure white in the dark facility. We rely on the cam's natural exposure instead.
    ///
    /// This is a SOFT dependency: everything is called via reflection, so the mod still
    /// loads and works (minus the remote view) if OBC isn't installed.
    /// </summary>
    public static class TurretBodyCam
    {
        private static bool _active;
        private static object? _mainCam; // OpenBodyCams.BodyCamComponent (a MonoBehaviour)
        private static GameObject? _mount;

        public static bool ObcAvailable => GetMainCam() != null;

        /// <summary>
        /// Point OBC's main body cam at the turret. We attach it to a mount placed a bit
        /// BEHIND the muzzle (in open space, not inside the wall the turret is bolted to),
        /// which removes most of the "see through walls" near-clipping. The mount is
        /// parented to the rotating rod, so the view follows the aim.
        /// </summary>
        public static void Show(Transform rod, Transform pivot, Transform muzzle)
        {
            var cam = GetMainCam();
            if (cam == null)
            {
                Plugin.Log.LogInfo("TurretBodyCam: OpenBodyCams not found; remote view unavailable.");
                return;
            }

            try
            {
                Vector3 barrelDir = (muzzle.position - pivot.position).normalized;

                if (_mount == null) _mount = new GameObject("AlliedDefenses_CamMount");
                _mount.transform.SetParent(rod, true);
                _mount.transform.position = muzzle.position - barrelDir * 0.8f + Vector3.up * 0.15f;
                _mount.transform.rotation = Quaternion.LookRotation(barrelDir, Vector3.up);

                var m = cam.GetType().GetMethod("SetTargetToTransform", new[] { typeof(Transform) });
                if (m == null) { Plugin.Log.LogWarning("TurretBodyCam: SetTargetToTransform not found."); return; }
                m.Invoke(cam, new object[] { _mount.transform });
                _mainCam = cam;
                _active = true;
                Plugin.Log.LogInfo("TurretBodyCam: OBC main body cam retargeted to the turret.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"TurretBodyCam.Show failed: {e.Message}");
            }
        }

        /// <summary>Restore OBC's main body cam to the local player.</summary>
        public static void Hide()
        {
            if (_mount != null) { UnityEngine.Object.Destroy(_mount); _mount = null; }
            if (!_active) return;
            _active = false;

            var cam = _mainCam ?? GetMainCam();
            _mainCam = null;
            if (cam == null) return;

            try
            {
                var player = StartOfRound.Instance != null ? StartOfRound.Instance.localPlayerController : null;
                var m = cam.GetType().GetMethod("SetTargetToPlayer");
                m?.Invoke(cam, new object[] { player });
                Plugin.Log.LogInfo("TurretBodyCam: OBC main body cam restored to the player.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"TurretBodyCam.Hide failed: {e.Message}");
            }
        }

        private static object? GetMainCam()
        {
            try
            {
                var apiType = AccessTools.TypeByName("OpenBodyCams.API.BodyCam");
                var prop = apiType?.GetProperty("MainBodyCam", BindingFlags.Public | BindingFlags.Static);
                return prop?.GetValue(null);
            }
            catch
            {
                return null;
            }
        }
    }
}

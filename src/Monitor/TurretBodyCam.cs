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
    /// heavy rendering.
    ///
    /// This is a SOFT dependency: everything is called via reflection, so the mod still
    /// loads and works (minus the remote view) if OBC isn't installed. Install
    /// OpenBodyCams (and have the body-cam available in the ship) to get the feed.
    /// </summary>
    public static class TurretBodyCam
    {
        private static bool _active;
        private static object? _mainCam; // OpenBodyCams.BodyCamComponent (a MonoBehaviour)

        public static bool ObcAvailable => GetMainCam() != null;

        /// <summary>Point OBC's main body cam at the turret muzzle.</summary>
        public static void Show(Transform aimPoint)
        {
            var cam = GetMainCam();
            if (cam == null)
            {
                Plugin.Log.LogInfo("TurretBodyCam: OpenBodyCams not found; remote view unavailable.");
                return;
            }

            try
            {
                var m = cam.GetType().GetMethod("SetTargetToTransform", new[] { typeof(Transform) });
                if (m == null) { Plugin.Log.LogWarning("TurretBodyCam: SetTargetToTransform not found."); return; }
                m.Invoke(cam, new object[] { aimPoint });
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

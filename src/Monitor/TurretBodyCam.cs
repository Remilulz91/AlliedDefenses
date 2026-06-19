using System;
using System.Reflection;
using AlliedDefenses.Config;
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
        private static GameObject? _lightObj;

        public static bool ObcAvailable => GetMainCam() != null;

        /// <summary>Point OBC's main body cam at the turret muzzle.</summary>
        public static void Show(Transform aimPoint)
        {
            AddLight(aimPoint); // illuminate the (dark) facility for the turret view

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
            RemoveLight();
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

        // The facility is dark and OBC's helper light follows players, not our turret
        // target, so we add our own light at the muzzle while controlling. Works in HDRP
        // via the HDAdditionalLightData component (set by reflection so we don't need a
        // compile-time HDRP reference).
        private static void AddLight(Transform aimPoint)
        {
            RemoveLight();
            if (!ModConfig.ManualControlLight.Value || aimPoint == null) return;

            try
            {
                _lightObj = new GameObject("AlliedDefenses_ControlLight");
                _lightObj.transform.SetParent(aimPoint, false);
                _lightObj.transform.localPosition = Vector3.zero;

                var light = _lightObj.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 40f;
                light.color = Color.white;
                light.intensity = 3f; // legacy fallback if HDRP data is absent

                float lumens = ModConfig.ManualControlLightIntensity.Value;
                var hdType = AccessTools.TypeByName("UnityEngine.Rendering.HighDefinition.HDAdditionalLightData");
                if (hdType != null)
                {
                    var hd = _lightObj.GetComponent(hdType) ?? _lightObj.AddComponent(hdType);
                    TrySet(hdType, hd, "intensity", lumens);
                    TrySet(hdType, hd, "range", 40f);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"TurretBodyCam.AddLight failed: {e.Message}");
            }
        }

        private static void TrySet(Type t, object instance, string prop, object value)
        {
            try { t.GetProperty(prop)?.SetValue(instance, value); } catch { /* optional */ }
        }

        private static void RemoveLight()
        {
            if (_lightObj != null) UnityEngine.Object.Destroy(_lightObj);
            _lightObj = null;
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

using System;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;
using AlliedDefenses.Config;

namespace AlliedDefenses.Beacon
{
    /// <summary>
    /// Builds the deployed Defense Beacon prefab at runtime: a small glowing pylon (primitives) on a
    /// GameObject that carries ONLY a NetworkObject (for reliable host->client replication) and a
    /// plain <see cref="BeaconObject"/> MonoBehaviour (look + ring + aura registration).
    ///
    /// Deliberately NOT a GrabbableObject / NetworkBehaviour: a bare NetworkObject replicates cleanly,
    /// while a runtime-built custom grabbable did not (reparent exceptions, behaviour-index errors,
    /// client crashes). The beacon is placed by the host and moved by re-spawning, not carried.
    /// </summary>
    public static class BeaconFactory
    {
        public static GameObject? Prefab { get; private set; }

        /// <summary>Create the prefab and register it as a network prefab. Idempotent.</summary>
        public static void EnsureBuilt()
        {
            if (Prefab != null) return;
            try
            {
                if (NetworkManager.Singleton == null)
                {
                    Plugin.Log.LogError("BeaconFactory: NetworkManager.Singleton null; cannot register beacon prefab.");
                    return;
                }

                Prefab = BuildPrefab();
                Prefab.SetActive(false); // template stays inactive; spawned copies are activated
                NetworkManager.Singleton.AddNetworkPrefab(Prefab);
                Plugin.Log.LogInfo("BeaconFactory: Defense Beacon prefab built and registered.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"BeaconFactory: failed to build beacon prefab: {e}");
            }
        }

        private static GameObject BuildPrefab()
        {
            var root = new GameObject("DefenseBeacon");
            UnityEngine.Object.DontDestroyOnLoad(root);
            root.hideFlags = HideFlags.HideAndDontSave;

            // ---- visual: a glowing pylon (cylinder body + emissive lamp), centred on the pivot ----
            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "BeaconBody";
            StripCollider(body);
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(0.35f, 0.5f, 0.35f);
            body.transform.localPosition = Vector3.zero;

            var top = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            top.name = "BeaconLamp";
            StripCollider(top);
            top.transform.SetParent(root.transform, false);
            top.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
            top.transform.localPosition = new Vector3(0f, 0.55f, 0f);

            var mainRenderer = body.GetComponent<MeshRenderer>();
            var lampRenderer = top.GetComponent<MeshRenderer>();
            TintEmissive(mainRenderer, ModConfig.AlliedColor, 0.12f);
            TintEmissive(lampRenderer, ModConfig.AlliedColor, 0.7f);

            var lightGo = new GameObject("BeaconLight");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = ModConfig.AlliedColor;
            light.range = 4f;
            light.intensity = 0.6f;

            // ---- network identity (bare NetworkObject: replicates existence + spawn transform) ----
            var netObj = root.AddComponent<NetworkObject>();
            AssignStableHash(netObj, "AlliedDefenses.DefenseBeacon");

            // ---- the (non-networked) behaviour: look, ring, aura registration ----
            root.AddComponent<BeaconObject>();

            return root;
        }

        // ---------- helpers ----------

        private static void StripCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c != null) UnityEngine.Object.Destroy(c);
        }

        private static void TintEmissive(Renderer r, Color color, float emission)
        {
            if (r == null) return;
            var mat = r.material;
            mat.color = color;
            try
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * emission);
            }
            catch { }
        }

        private static void AssignStableHash(NetworkObject netObj, string key)
        {
            uint hash = (uint)key.GetHashCode();
            var field = typeof(NetworkObject).GetField(
                "GlobalObjectIdHash",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null)
            {
                Plugin.Log.LogError("BeaconFactory: GlobalObjectIdHash field not found; spawn will fail.");
                return;
            }
            field.SetValue(netObj, hash);
        }
    }
}

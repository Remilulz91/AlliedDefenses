using System;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;
using AlliedDefenses.Config;

namespace AlliedDefenses.Beacon
{
    /// <summary>
    /// Builds the Defense Beacon prefab ENTIRELY AT RUNTIME (no asset bundle): a small glowing
    /// pylon made from Unity primitives, wired up as a grabbable networked item.
    ///
    /// Pieces required for a working Lethal Company grabbable:
    ///   - a NetworkObject with a stable GlobalObjectIdHash (so host/clients agree on the prefab),
    ///   - a Rigidbody + colliders (one non-trigger for resting on the floor, one trigger for the
    ///     grab ray), on the "Props" layer so the interact ray sees it,
    ///   - a MeshRenderer (mainObjectRenderer) and the BeaconItem : GrabbableObject component,
    ///   - an Item ScriptableObject describing weight / two-handedness, assigned to itemProperties.
    ///
    /// This is the trickiest part of the mod and CANNOT be verified without running the game, so
    /// every step logs and the caller guards. If grabbing/'two-handed' misbehaves in-game, the
    /// BepInEx log from here tells us which piece to adjust.
    /// </summary>
    public static class BeaconFactory
    {
        public static GameObject? Prefab { get; private set; }
        public static Item? ItemDef { get; private set; }

        private const string BeaconName = "Defense Beacon";

        /// <summary>Create the Item scriptable + prefab and register it as a network prefab. Idempotent.</summary>
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

                var item = ScriptableObject.CreateInstance<Item>();
                item.itemName = BeaconName;
                item.twoHanded = true;
                item.twoHandedAnimation = true;
                item.canBeGrabbedBeforeGameStart = true;   // allowed to sit in the ship pre-landing
                item.itemSpawnsOnGround = true;
                item.isScrap = false;
                item.creditsWorth = 0;
                item.weight = Core.UpgradeManager.BeaconWeight(); // heavy; the 'haul' upgrade lowers it
                item.requiresBattery = false;
                item.automaticallySetUsingPower = false;
                item.saveItemVariable = false;
                item.allowDroppingAheadOfPlayer = true;
                item.floorYOffset = 0;
                item.verticalOffset = 0.6f;
                item.rotationOffset = Vector3.zero;
                item.positionOffset = Vector3.zero;
                item.meshVariants = Array.Empty<Mesh>();
                item.materialVariants = Array.Empty<Material>();
                item.toolTips = Array.Empty<string>();
                ItemDef = item;

                var root = BuildPrefab(item);
                item.spawnPrefab = root;
                Prefab = root;

                // CRITICAL: keep the template INACTIVE. If it stays active its GrabbableObject.Update
                // runs every frame even in the menu (no StartOfRound / no floor), which throws a
                // NullReference in FallWithCurve on a loop. Spawned copies are re-activated in
                // BeaconManager.SpawnIfMissing before NetworkObject.Spawn.
                root.SetActive(false);

                NetworkManager.Singleton.AddNetworkPrefab(root);
                Plugin.Log.LogInfo("BeaconFactory: Defense Beacon prefab built and registered.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"BeaconFactory: failed to build beacon prefab: {e}");
            }
        }

        private static GameObject BuildPrefab(Item item)
        {
            // Root object: physics body + network identity + the grabbable behaviour.
            var root = new GameObject("DefenseBeacon");
            UnityEngine.Object.DontDestroyOnLoad(root);
            root.hideFlags = HideFlags.HideAndDontSave;
            SetLayerSafe(root, "Props");

            // ---- visual: a glowing pylon (cylinder body + emissive top) ----
            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "BeaconBody";
            StripCollider(body);
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(0.35f, 0.5f, 0.35f); // ~1m tall
            body.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            SetLayerSafe(body, "Props");

            var top = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            top.name = "BeaconLamp";
            StripCollider(top);
            top.transform.SetParent(root.transform, false);
            top.transform.localScale = new Vector3(0.4f, 0.4f, 0.4f);
            top.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            SetLayerSafe(top, "Props");

            var mainRenderer = body.GetComponent<MeshRenderer>();
            var lampRenderer = top.GetComponent<MeshRenderer>();
            TintEmissive(mainRenderer, ModConfig.AlliedColor, 0.4f);
            TintEmissive(lampRenderer, ModConfig.AlliedColor, 2.5f);

            // A soft light so the beacon reads at a glance in the dark.
            var lightGo = new GameObject("BeaconLight");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = ModConfig.AlliedColor;
            light.range = 6f;
            light.intensity = 3f;

            // ---- physics/interaction colliders ----
            // Non-trigger box so it rests on the floor when dropped.
            var solid = root.AddComponent<BoxCollider>();
            solid.center = new Vector3(0f, 0.5f, 0f);
            solid.size = new Vector3(0.7f, 1.0f, 0.7f);

            // Trigger box used by the grab ray (GrabbableObject wants a trigger collider).
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 0.6f, 0f);
            trigger.size = new Vector3(0.9f, 1.3f, 0.9f);

            var body3d = root.AddComponent<Rigidbody>();
            body3d.mass = 1f;
            body3d.isKinematic = true;       // GrabbableObject drives resting position itself
            body3d.useGravity = false;
            body3d.interpolation = RigidbodyInterpolation.Interpolate;

            // ---- network identity ----
            var netObj = root.AddComponent<NetworkObject>();
            AssignStableHash(netObj, "AlliedDefenses.DefenseBeacon");

            // ---- the grabbable behaviour ----
            var beacon = root.AddComponent<BeaconItem>();
            beacon.itemProperties = item;
            beacon.grabbable = true;
            beacon.grabbableToEnemies = false;
            beacon.mainObjectRenderer = mainRenderer;
            beacon.propColliders = new Collider[] { solid, trigger };
            beacon.useCooldown = 0f;

            return root;
        }

        // ---------- helpers ----------

        private static void StripCollider(GameObject go)
        {
            var c = go.GetComponent<Collider>();
            if (c != null) UnityEngine.Object.Destroy(c);
        }

        private static void SetLayerSafe(GameObject go, string layerName)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer >= 0) go.layer = layer;
        }

        private static void TintEmissive(Renderer r, Color color, float emission)
        {
            if (r == null) return;
            // Instance material so we don't touch the shared primitive material.
            var mat = r.material;
            mat.color = color;
            try
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * emission);
            }
            catch { /* shader without emission -> ignore */ }
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

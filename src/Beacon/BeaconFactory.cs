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
    ///   - a Rigidbody + a non-trigger collider on layer 8 and tagged "PhysicsProp" (both are
    ///     mandatory: PlayerControllerB.BeginGrabObject rejects anything else),
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

        // Lethal Company's grabbable-item layer is 6 ("Props"). It is in interactableObjectsMask
        // (0x40000340) so the interact ray hits it, and it is NOT one of the layers (8 = ship
        // geometry, 30) that the grab code treats as "not a grabbable" before it ever checks the
        // "PhysicsProp" tag. Putting the beacon on layer 8 (ship geometry) was why it never grabbed.
        private const int GrabbableLayer = 6;

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
                item.verticalOffset = 0.5f; // pivot rests 0.5 m above the floor (see BuildPrefab)
                item.rotationOffset = Vector3.zero;
                // Held offset relative to the player's item holder. The model is centred on its
                // pivot, so a small forward + slight-up offset makes it sit naturally in view
                // instead of being shoved off the bottom-right of the screen.
                item.positionOffset = new Vector3(0f, 0.1f, 0.25f);
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
            // Grabbing needs the collider's GameObject on layer 6 ("Props") AND tagged "PhysicsProp".
            root.layer = GrabbableLayer;
            try { root.tag = "PhysicsProp"; }
            catch { Plugin.Log.LogWarning("BeaconFactory: 'PhysicsProp' tag missing; beacon may not be grabbable."); }

            // ---- visual: a glowing pylon (cylinder body + emissive top) ----
            // NOTE: everything is centred on the ROOT pivot (localY 0), so the pivot sits in the
            // MIDDLE of the beacon, ~0.5 m off the floor when resting. This is what makes the game's
            // line-of-sight grab check succeed (a floor-level pivot gets blocked by the floor).
            var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "BeaconBody";
            StripCollider(body);
            body.transform.SetParent(root.transform, false);
            body.transform.localScale = new Vector3(0.35f, 0.5f, 0.35f); // ~1m tall, spans -0.5..+0.5
            body.transform.localPosition = new Vector3(0f, 0f, 0f);

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

            // A soft light so the beacon reads at a glance in the dark (kept dim so it doesn't
            // wash the whole room green like the first build did).
            var lightGo = new GameObject("BeaconLight");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 0.1f, 0f); // near the pivot / model centre
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = ModConfig.AlliedColor;
            light.range = 4f;
            light.intensity = 0.6f;

            // ---- physics/interaction collider ----
            // One non-trigger box: it rests on the floor AND is what the grab ray hits. It lives on
            // the root, so the ray sees root.layer (8) and root.tag ("PhysicsProp").
            var solid = root.AddComponent<BoxCollider>();
            solid.center = new Vector3(0f, 0f, 0f);       // centred on the pivot
            solid.size = new Vector3(0.7f, 1.0f, 0.7f);   // spans -0.5..+0.5 in Y

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
            beacon.propColliders = new Collider[] { solid };
            beacon.useCooldown = 0f;

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
            Plugin.Log.LogInfo($"BeaconFactory: beacon network prefab hash = {hash} (must match host & client).");
        }
    }
}

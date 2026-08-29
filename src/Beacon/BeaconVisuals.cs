using System.Collections.Generic;
using UnityEngine;

namespace AlliedDefenses.Beacon
{
    /// <summary>
    /// Gives the runtime-built beacon a real look by borrowing a vanilla item's mesh + inventory
    /// icon (no asset bundle needed). Applied once per instance at runtime, when StartOfRound (and
    /// therefore the item database) exists. Everything is guarded: if anything is missing we simply
    /// keep the plain primitive so the beacon still works.
    /// </summary>
    public static class BeaconVisuals
    {
        // Preferred donor items (thematic, glowing/placeable devices), matched by name substring.
        private static readonly string[] Preferred =
            { "radar", "lamp", "control", "cash register", "apparatus", "jar" };

        public static void ApplyVanillaLook(GrabbableObject beacon)
        {
            try
            {
                var sor = StartOfRound.Instance;
                var list = sor != null && sor.allItemsList != null ? sor.allItemsList.itemsList : null;
                if (list == null || list.Count == 0) return;

                Item src = PickSource(list);
                if (src == null) return;

                if (src.itemIcon != null && beacon.itemProperties != null)
                    beacon.itemProperties.itemIcon = src.itemIcon;

                var srcMf = src.spawnPrefab != null ? src.spawnPrefab.GetComponentInChildren<MeshFilter>() : null;
                if (srcMf == null || srcMf.sharedMesh == null) return;
                var srcMr = srcMf.GetComponent<MeshRenderer>();

                var bodyT = beacon.transform.Find("BeaconBody");
                var mf = bodyT != null ? bodyT.GetComponent<MeshFilter>() : null;
                var mr = bodyT != null ? bodyT.GetComponent<MeshRenderer>() : null;
                if (mf == null) return;

                mf.sharedMesh = srcMf.sharedMesh;
                if (mr != null && srcMr != null) mr.sharedMaterials = srcMr.sharedMaterials;

                // Normalise the donor mesh to ~0.7 m and centre it on the pivot (needed for the
                // grab line-of-sight and the held offset to keep working).
                var b = srcMf.sharedMesh.bounds;
                float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                float s = maxDim > 0.001f ? 0.7f / maxDim : 1f;
                bodyT.localScale = Vector3.one * s;
                bodyT.localPosition = -b.center * s;
                bodyT.localRotation = Quaternion.identity;

                // Hide the primitive lamp sphere; keep the point light for the glow.
                var lamp = beacon.transform.Find("BeaconLamp");
                var lampMr = lamp != null ? lamp.GetComponent<MeshRenderer>() : null;
                if (lampMr != null) lampMr.enabled = false;

                Plugin.Log.LogInfo($"BeaconVisuals: using '{src.itemName}' model/icon for the beacon.");
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"BeaconVisuals: could not apply vanilla look ({e.Message}); keeping primitive.");
            }
        }

        private static Item PickSource(List<Item> list)
        {
            Item fallback = null;
            foreach (var it in list)
            {
                if (it == null || it.spawnPrefab == null) continue;
                if (it.spawnPrefab.GetComponentInChildren<MeshFilter>() == null) continue;
                if (fallback == null && it.itemIcon != null) fallback = it;

                string n = (it.itemName ?? "").ToLowerInvariant();
                foreach (var p in Preferred)
                    if (n.Contains(p)) return it;
            }
            return fallback;
        }
    }
}

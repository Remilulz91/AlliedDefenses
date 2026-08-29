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
        // Preferred donor items, in PRIORITY order. Compact, upright, beacon-ish props first.
        // (The Apparatus was dropped: its mesh is long on Z, so it normalised to a thin object
        // lying down — which looked offset from the light and was near-invisible when held.)
        private static readonly string[] Preferred =
            { "fancy lamp", "lamp", "flask", "jar", "control pad", "cash register", "gift" };

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

                // Normalise the donor mesh to ~0.7 m (looks right resting on the floor).
                var b = srcMf.sharedMesh.bounds;
                float maxDim = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
                float s = maxDim > 0.001f ? 0.7f / maxDim : 1f;
                bodyT.localRotation = Quaternion.identity;
                bodyT.localScale = Vector3.one * s;
                bodyT.localPosition = Vector3.zero;

                // Align the mesh centre to the beacon pivot DETERMINISTICALLY (renderer.bounds is
                // cached and still reports the old primitive right after swapping the mesh). We ask
                // the transform where the mesh's bounds-centre currently is in world space, then
                // shift the body so that point lands exactly on the pivot.
                Vector3 meshCenterWorld = bodyT.TransformPoint(b.center);
                bodyT.position += beacon.transform.position - meshCenterWorld;

                // Hide the primitive lamp sphere; keep the point light for the glow.
                var lamp = beacon.transform.Find("BeaconLamp");
                var lampMr = lamp != null ? lamp.GetComponent<MeshRenderer>() : null;
                if (lampMr != null) lampMr.enabled = false;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"BeaconVisuals: could not apply vanilla look ({e.Message}); keeping primitive.");
            }
        }

        private static Item PickSource(List<Item> list)
        {
            // Try each preferred name in PRIORITY order, so "fancy lamp" wins over later options.
            foreach (var term in Preferred)
                foreach (var it in list)
                    if (IsUsable(it) && (it.itemName ?? "").ToLowerInvariant().Contains(term))
                        return it;

            // Fallback: the first usable item that has an icon and a reasonably compact mesh.
            foreach (var it in list)
                if (IsUsable(it) && it.itemIcon != null && !IsLongMesh(it))
                    return it;
            foreach (var it in list)
                if (IsUsable(it) && it.itemIcon != null)
                    return it;
            return null;
        }

        private static bool IsUsable(Item it) =>
            it != null && it.spawnPrefab != null &&
            it.spawnPrefab.GetComponentInChildren<MeshFilter>() != null;

        /// <summary>True if the mesh is very elongated (one axis dominates), which normalises badly.</summary>
        private static bool IsLongMesh(Item it)
        {
            var mf = it.spawnPrefab.GetComponentInChildren<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return true;
            var s = mf.sharedMesh.bounds.size;
            float max = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
            float min = Mathf.Min(s.x, Mathf.Min(s.y, s.z));
            return max > min * 3f; // 3:1 or longer -> skip
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace AlliedDefenses.Core
{
    /// <summary>
    /// Live directory of every placed "defense beacon" in the world.
    ///
    /// A beacon is an inert, buy-once, placeable ship object (see <see cref="Beacon.BeaconObject"/>)
    /// that acts as an ALLIED-DEFENSE ANCHOR OUTSIDE the facility — near the ship — where there
    /// are normally no turrets or mines. The protective auras of the mod (Ghost Girl sanity drain,
    /// sand-worm untargetable, Eyeless-Dog noise muffle) all key off "is the player near an allied
    /// defense?", so registering a beacon's position here makes those auras work around the ship.
    ///
    /// Beacons can be moved/stored by the game's own build mode, so we keep live Transform
    /// references and read their CURRENT position on every query rather than caching coordinates.
    /// Every client tracks its own beacons (the game replicates the placed objects), so this needs
    /// no networking of its own.
    /// </summary>
    public static class BeaconRegistry
    {
        private static readonly List<Transform> _beacons = new();

        public static int Count => _beacons.Count;

        public static void Register(Transform t)
        {
            if (t == null || _beacons.Contains(t)) return;
            _beacons.Add(t);
        }

        public static void Unregister(Transform t)
        {
            if (t == null) return;
            _beacons.Remove(t);
        }

        /// <summary>True if any active beacon is within <paramref name="radius"/> of a point.</summary>
        public static bool AnyBeaconWithin(Vector3 point, float radius)
        {
            if (radius <= 0f) return false;
            float r2 = radius * radius;
            // Iterate backwards so we can prune any beacon whose object was destroyed.
            for (int i = _beacons.Count - 1; i >= 0; i--)
            {
                var t = _beacons[i];
                if (t == null) { _beacons.RemoveAt(i); continue; } // Unity "== null" catches destroyed objects
                if ((t.position - point).sqrMagnitude <= r2) return true;
            }
            return false;
        }
    }
}

using AlliedDefenses.Config;
using UnityEngine;

namespace AlliedDefenses.Core
{
    /// <summary>
    /// Shared targeting helpers used by allied defenses: find the most relevant enemy
    /// (monster) in an area, check line of sight, etc.
    ///
    /// Confirmed game references:
    ///   - The list of living monsters is RoundManager.Instance.SpawnedEnemies
    ///     (List&lt;EnemyAI&gt;). Each EnemyAI has a transform and an "isEnemyDead" bool.
    ///   - The line-of-sight raycast uses a layer mask that blocks on walls/rooms.
    ///     The integer 1051400 is the mask used by working turret mods (Room +
    ///     Colliders + Default + Player...). For wall-only blocking you may prefer
    ///     StartOfRound.Instance.collidersAndRoomMaskAndDefault.
    /// </summary>
    public static class TargetingHelper
    {
        // Layer mask proven to work for turret line-of-sight in shipped turret mods.
        private const int LineOfSightMask = 1051400;

        /// <summary>
        /// Find the closest living enemy to 'origin', within 'range' meters, optionally
        /// constrained to a cone of half-angle 'coneHalfAngle' around 'forward', and
        /// visible (raycast with no obstacle). Returns null if no valid enemy.
        /// </summary>
        public static EnemyAI? FindBestEnemy(Vector3 origin, Vector3 forward, float range,
                                             float coneHalfAngle = 180f, bool requireLineOfSight = true)
        {
            var rm = RoundManager.Instance;
            if (rm == null || rm.SpawnedEnemies == null) return null;

            EnemyAI? best = null;
            float bestDist = float.MaxValue;
            float rangeSqr = range * range;

            foreach (var enemy in rm.SpawnedEnemies)
            {
                if (enemy == null || enemy.isEnemyDead) continue;

                Vector3 enemyPos = enemy.transform.position;
                Vector3 to = enemyPos - origin;

                float distSqr = to.sqrMagnitude;
                if (distSqr > rangeSqr) continue;

                if (coneHalfAngle < 180f && Vector3.Angle(forward, to) > coneHalfAngle)
                    continue;

                if (requireLineOfSight && !HasLineOfSight(origin, enemyPos)) continue;

                if (distSqr < bestDist)
                {
                    bestDist = distSqr;
                    best = enemy;
                }
            }

            return best;
        }

        /// <summary>True if there is no wall between 'from' and 'to'.</summary>
        public static bool HasLineOfSight(Vector3 from, Vector3 to)
        {
            Vector3 dir = to - from;
            float dist = dir.magnitude;
            if (dist < 0.01f) return true;

            // If the ray hits something BEFORE reaching the target -> no line of sight.
            return !Physics.Raycast(from, dir.normalized, dist - 0.5f, LineOfSightMask,
                                    QueryTriggerInteraction.Ignore);
        }

        /// <summary>User-configured detection range.</summary>
        public static float Range => ModConfig.EnemyDetectionRange.Value;
    }
}

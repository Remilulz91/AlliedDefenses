using System;
using System.Collections.Generic;
using AlliedDefenses.Config;
using AlliedDefenses.Core;
using Unity.Netcode;
using UnityEngine;

namespace AlliedDefenses.Defenses
{
    /// <summary>
    /// "Turret" module. Implements IHijackableDefense and contains the aiming/firing
    /// logic toward enemies (called by the Harmony patches).
    ///
    /// Game member names below are CONFIRMED against the vanilla decompiled Turret
    /// class (verified via the lc-turret-key and MissileTurret mod sources):
    ///   - bool turretActive
    ///   - TurretMode turretMode (enum, value Detection / Firing / ...)
    ///   - float rotationSpeed
    ///   - Animator turretAnimator   (SetInteger("TurretMode", n))
    ///   - ParticleSystem bulletParticles
    ///   - Transform centerPoint     (turret pivot / position origin)
    ///   - void ToggleTurretEnabledLocalClient(bool enabled)
    /// EnemyAI.HitEnemy(int, PlayerControllerB, bool, int) and EnemyAI.isEnemyDead are
    /// also confirmed. Resolving a turret from its terminal id is done by the shared
    /// TerminalCodeResolver (the only place still using defensive reflection, for the
    /// TerminalAccessibleObject code field).
    /// </summary>
    public class TurretHijack : IHijackableDefense
    {
        public string TypeId => "turret";
        public string DisplayName => "Turret";
        public Type ComponentType => typeof(Turret);

        // Allied fire cadence, per turret (key = Unity instanceID).
        private static readonly Dictionary<int, float> _nextFire = new();
        // Barrel direction captured when the turret becomes allied (idle-scan centre).
        private static readonly Dictionary<int, Vector3> _baseDir = new();
        private const float FireInterval = 0.21f;   // matches the vanilla fire rate
        private const int EnemyDamagePerShot = 1;   // damage dealt to monsters per shot (tune to taste)
        private const float AlignToleranceDeg = 10f; // max angle to consider the target "on aim"

        // ----------------------------------------------------------------
        // Resolve by terminal code (e.g. "A0"), via the shared resolver.
        // ----------------------------------------------------------------
        public bool TryResolveByTerminalCode(string code, out Component? defense)
        {
            defense = TerminalCodeResolver.Resolve(code, typeof(Turret));
            return defense != null;
        }

        // ----------------------------------------------------------------
        // Apply the allied state to a specific turret.
        // ----------------------------------------------------------------
        public void ApplyAlliedState(Component defense, bool allied)
        {
            if (defense is not Turret turret) return;

            if (allied)
            {
                // Make sure the turret is powered on and in its scanning mode so it
                // looks "awake" while we drive it toward enemies.
                turret.turretActive = true;
                turret.turretMode = TurretMode.Detection;
                if (turret.turretAnimator != null)
                    turret.turretAnimator.SetInteger("TurretMode", (int)TurretMode.Detection);

                TurretVisuals.SetAllied(turret, true);  // green laser/light cue

                // Remember the current barrel direction as the centre of the idle scan,
                // and log the turret structure once (diagnostic).
                if (turret.aimPoint != null && turret.centerPoint != null)
                    _baseDir[turret.GetInstanceID()] =
                        (turret.aimPoint.position - turret.centerPoint.position).normalized;
                LogHierarchyOnce(turret);
            }
            else
            {
                int id = turret.GetInstanceID();
                _nextFire.Remove(id);
                _baseDir.Remove(id);
                TurretVisuals.SetAllied(turret, false); // restore original red/orange
                // Vanilla Update resumes on its own next frame (patch stops bypassing).
            }
        }

        // The turret has its own loop (handled by patches) -> nothing to do here.
        public void TickAlliedTargeting(Component defense) { }

        // ================================================================
        //  ALLIED AIMING / FIRING  (called by TurretPatches on Update)
        // ================================================================

        /// <summary>
        /// While a turret is allied: find the best enemy, rotate the turret toward it,
        /// and fire (deal damage) when aligned and in range.
        ///
        /// Rotation runs on every client for a consistent visual (enemy positions are
        /// network-synced). Damage is applied on the HOST only, to avoid each client
        /// dealing the hit separately (which would multiply the damage).
        /// </summary>
        public void DriveAlliedTurret(Turret turret)
        {
            // Real nodes (confirmed from the prefab diagnostic):
            //   turretRod  = RotatingRodContainer  -> the part that actually rotates
            //   centerPoint= CenterTurretPos       -> rotation pivot (reference point)
            //   aimPoint   = GunBarrelPos          -> the muzzle
            if (!GetNodes(turret, out var rod, out var pivot, out var muzzle))
                return; // can't safely drive this turret

            // If a player is manually driving this turret, obey their aim/fire instead
            // of auto-targeting enemies.
            ulong? netId = HijackManager.ResolveNetworkId(turret);
            if (netId.HasValue && TurretControlSession.Get(netId.Value) is ControlInfo ctrl)
            {
                DriveManually(turret, rod, pivot, muzzle, ctrl);
                return;
            }

            Vector3 barrelDir = (muzzle.position - pivot.position).normalized;
            var enemy = TargetingHelper.FindBestEnemy(
                muzzle.position, barrelDir,
                range: ModConfig.EnemyDetectionRange.Value,
                coneHalfAngle: 180f,        // an allied turret may swivel toward the threat
                requireLineOfSight: true);

            if (enemy == null)
            {
                IdleScan(turret, rod, pivot, muzzle); // gentle sweep so it looks alive
                TurretVisuals.HideBeam(turret);       // no target -> no beam
                return;
            }

            Vector3 targetPoint = enemy.transform.position + Vector3.up * 0.5f;

            // Rotate the rotating rod toward the enemy (visual, all clients).
            float speed = Mathf.Max(turret.rotationSpeed, 90f);
            AimRodAt(rod, pivot.position, muzzle.position, targetPoint, speed);

            // Permanent green beam from the muzzle.
            UpdateBeam(turret, pivot, muzzle);

            // Fire if aligned (host applies the damage).
            bool isServer = NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;
            float ang = Vector3.Angle(muzzle.position - pivot.position, targetPoint - pivot.position);
            if (isServer && ang <= AlignToleranceDeg)
                TryFireAtEnemy(turret, enemy);
        }

        /// <summary>Resolve the rotating rod, pivot and muzzle (publicized fields).</summary>
        private static bool GetNodes(Turret turret, out Transform rod, out Transform pivot, out Transform muzzle)
        {
            rod = turret.turretRod;
            pivot = turret.centerPoint;
            muzzle = turret.aimPoint;
            return rod != null && pivot != null && muzzle != null;
        }

        /// <summary>
        /// Rotate the rod so the barrel direction (pivot -> muzzle) points at targetPoint.
        /// Uses FromToRotation, so it works regardless of the rod's local axes, and steps
        /// at most degPerSec per second for a smooth turn.
        /// </summary>
        private static void AimRodAt(Transform rod, Vector3 pivotPos, Vector3 muzzlePos,
                                     Vector3 targetPoint, float degPerSec)
        {
            Vector3 cur = muzzlePos - pivotPos;
            Vector3 des = targetPoint - pivotPos;
            if (cur.sqrMagnitude < 1e-5f || des.sqrMagnitude < 1e-5f) return;

            Quaternion targetRot = Quaternion.FromToRotation(cur.normalized, des.normalized) * rod.rotation;
            rod.rotation = Quaternion.RotateTowards(rod.rotation, targetRot, degPerSec * Time.deltaTime);
        }

        /// <summary>
        /// Gentle left-right sweep around the barrel direction captured when the turret
        /// was hijacked, so an allied turret with no target looks alive instead of frozen.
        /// </summary>
        private static void IdleScan(Turret turret, Transform rod, Transform pivot, Transform muzzle)
        {
            int id = turret.GetInstanceID();
            Vector3 baseDir = _baseDir.TryGetValue(id, out var d)
                ? d : (muzzle.position - pivot.position).normalized;

            float yaw = Mathf.Sin(Time.time * 0.6f) * 50f;            // +/- 50 deg sweep
            Vector3 des = Quaternion.AngleAxis(yaw, Vector3.up) * baseDir;
            AimRodAt(rod, pivot.position, muzzle.position, pivot.position + des * 5f, 60f);
        }

        private static bool _diagLogged;

        /// <summary>
        /// One-time diagnostic (first hijacked turret only): dumps the whole turret
        /// PREFAB tree (starting a couple of parents up) and, crucially, every Transform
        /// field on the Turret component via reflection — that tells us exactly which
        /// node the game rotates so we can drive the same one.
        /// </summary>
        private static void LogHierarchyOnce(Turret turret)
        {
            if (_diagLogged) return;
            _diagLogged = true;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[Turret diagnostic] PREFAB TREE (R=Renderer L=Light Ln=LineRenderer T=Turret):");

            // Start a couple of parents up to capture the prefab root (the mesh is a
            // sibling of TurretScript), but cap depth/count so we never dump the whole level.
            Transform start = turret.transform;
            for (int i = 0; i < 2 && start.parent != null; i++) start = start.parent;
            int count = 0;
            DumpTree(start, 0, sb, ref count);

            sb.AppendLine("[Turret diagnostic] Transform fields on Turret:");
            foreach (var f in typeof(Turret).GetFields(
                         System.Reflection.BindingFlags.Instance |
                         System.Reflection.BindingFlags.Public |
                         System.Reflection.BindingFlags.NonPublic))
            {
                if (!typeof(Transform).IsAssignableFrom(f.FieldType)) continue;
                var val = f.GetValue(turret) as Transform;
                sb.AppendLine($"  {f.Name} -> {(val != null ? val.name : "null")}");
            }

            Plugin.Log.LogInfo(sb.ToString());
        }

        private static void DumpTree(Transform t, int depth, System.Text.StringBuilder sb, ref int count)
        {
            if (count++ > 80 || depth > 5) return; // safety caps
            string tags = "";
            if (t.GetComponent<Renderer>() != null) tags += "R";
            if (t.GetComponent<Light>() != null) tags += "L";
            if (t.GetComponent<LineRenderer>() != null) tags += "Ln";
            if (t.GetComponent<Turret>() != null) tags += "T";
            sb.AppendLine($"{new string(' ', depth * 2)}{t.name} [{tags}]");
            foreach (Transform child in t) DumpTree(child, depth + 1, sb, ref count);
        }

        /// <summary>
        /// Draw the allied beam from the barrel along its forward axis, stopping at the
        /// first surface it hits (or at detection range). Gives a clear green laser
        /// while the turret is on our side.
        /// </summary>
        private static void UpdateBeam(Turret turret, Transform pivot, Transform muzzle)
        {
            float range = ModConfig.EnemyDetectionRange.Value;
            Vector3 dir = (muzzle.position - pivot.position).normalized;
            Vector3 start = muzzle.position;
            Vector3 end = Physics.Raycast(start, dir, out RaycastHit hit, range, ~0,
                                          QueryTriggerInteraction.Ignore)
                ? hit.point
                : start + dir * range;

            TurretVisuals.DrawBeam(turret, start, end);
        }

        private void TryFireAtEnemy(Turret turret, EnemyAI enemy)
        {
            int id = turret.GetInstanceID();
            float now = Time.time;
            if (_nextFire.TryGetValue(id, out float next) && now < next) return;
            _nextFire[id] = now + FireInterval;

            // Confirmed signature: HitEnemy(int force, PlayerControllerB playerWhoHit,
            //                               bool playHitSFX, int hitID).
            enemy.HitEnemy(EnemyDamagePerShot, null, false, -1);

            // Cosmetic muzzle flash if the particle system is present.
            if (turret.bulletParticles != null)
                turret.bulletParticles.Play();
        }

        // ================================================================
        //  MANUAL REMOTE CONTROL DRIVING
        // ================================================================

        /// <summary>
        /// Point the turret exactly where the controlling player aims, and fire (at
        /// ANYTHING, players included) while they hold fire. Aim is applied on every
        /// client for a consistent visual; damage is resolved on the host only.
        /// </summary>
        private void DriveManually(Turret turret, Transform rod, Transform pivot, Transform muzzle, ControlInfo ctrl)
        {
            if (ctrl.AimDirection.sqrMagnitude > 0.001f)
            {
                Vector3 targetPoint = pivot.position + ctrl.AimDirection.normalized * 10f;
                AimRodAt(rod, pivot.position, muzzle.position, targetPoint, 720f); // snappy
            }

            // Always show the green beam while manually aiming.
            UpdateBeam(turret, pivot, muzzle);

            bool isServer = NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;
            if (ctrl.Firing && isServer)
                ManualFire(turret, pivot, muzzle);
        }

        private void ManualFire(Turret turret, Transform pivot, Transform muzzle)
        {
            int id = turret.GetInstanceID();
            float now = Time.time;
            if (_nextFire.TryGetValue(id, out float next) && now < next) return;
            _nextFire[id] = now + FireInterval;

            int damage = ModConfig.ManualControlDamage.Value;
            Vector3 dir = (muzzle.position - pivot.position).normalized;
            Vector3 origin = muzzle.position;

            // Hit the first thing in the line of fire (broad mask; we filter by what we hit).
            if (Physics.Raycast(origin, dir, out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore))
            {
                var enemy = hit.collider.GetComponentInParent<EnemyAI>();
                if (enemy != null && !enemy.isEnemyDead)
                {
                    enemy.HitEnemy(damage, null, false, -1);
                }
                else
                {
                    var player = hit.collider.GetComponentInParent<GameNetcodeStuff.PlayerControllerB>();
                    if (player != null)
                        DamagePlayer(player, damage);
                }
            }

            if (turret.bulletParticles != null)
                turret.bulletParticles.Play();
        }

        /// <summary>
        /// Deal damage to a player. PlayerControllerB.DamagePlayer has a long optional
        /// signature that has shifted between game versions, so we call it defensively
        /// by reflection and fall back to other known damage methods.
        /// TODO: confirm the exact signature in your decompiler and switch to a direct call.
        /// </summary>
        private static void DamagePlayer(GameNetcodeStuff.PlayerControllerB player, int damage)
        {
            try
            {
                // Common signature: DamagePlayer(int damageNumber, bool hasDamageSFX,
                //   bool callRPC, int causeOfDeath, int deathAnimation, bool fallDamage, Vector3 force)
                HarmonyLib.Traverse.Create(player).Method("DamagePlayer",
                    damage, true, true, 0, 0, false, Vector3.zero).GetValue();
            }
            catch
            {
                try { HarmonyLib.Traverse.Create(player).Method("DamagePlayer", damage).GetValue(); }
                catch (System.Exception e) { Plugin.Log.LogWarning($"DamagePlayer failed: {e.Message}"); }
            }
        }
    }
}

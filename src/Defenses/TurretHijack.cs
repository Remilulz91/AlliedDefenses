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
        // Facing captured when the turret becomes allied, used as the idle-scan centre.
        private static readonly Dictionary<int, Quaternion> _baseRotation = new();
        // Turrets whose transform hierarchy we've already logged (diagnostic).
        private static readonly HashSet<int> _loggedHierarchy = new();
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

                // Remember the current facing as the centre of the idle scan, and log
                // the turret's transform hierarchy once (helps pick the real rotation node).
                Transform pivot0 = turret.centerPoint != null ? turret.centerPoint : turret.transform;
                _baseRotation[turret.GetInstanceID()] = pivot0.rotation;
                LogHierarchyOnce(turret);
            }
            else
            {
                int id = turret.GetInstanceID();
                _nextFire.Remove(id);
                _baseRotation.Remove(id);
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
            Transform pivot = turret.centerPoint != null ? turret.centerPoint : turret.transform;
            Vector3 origin = pivot.position;

            // If a player is manually driving this turret, obey their aim/fire instead
            // of auto-targeting enemies.
            ulong? netId = HijackManager.ResolveNetworkId(turret);
            if (netId.HasValue && TurretControlSession.Get(netId.Value) is ControlInfo ctrl)
            {
                DriveManually(turret, pivot, ctrl);
                return;
            }

            var enemy = TargetingHelper.FindBestEnemy(
                origin, pivot.forward,
                range: ModConfig.EnemyDetectionRange.Value,
                coneHalfAngle: 180f,        // an allied turret may swivel 360° toward the threat
                requireLineOfSight: true);

            if (enemy == null)
            {
                IdleScan(turret, pivot);        // gently sweep so it looks alive, not frozen
                TurretVisuals.HideBeam(turret); // no target -> no beam
                return;
            }

            Vector3 toEnemy = (enemy.transform.position + Vector3.up * 0.5f) - origin;

            // 1) Rotate the turret toward the enemy (visual, all clients).
            Quaternion target = Quaternion.LookRotation(toEnemy.normalized);
            float speed = Mathf.Max(turret.rotationSpeed, 90f); // never feel sluggish
            pivot.rotation = Quaternion.RotateTowards(pivot.rotation, target, speed * Time.deltaTime);

            // 2) Permanent green beam from the barrel along where it's aiming.
            UpdateBeam(turret, pivot);

            // 3) Fire if aligned (host applies the damage).
            bool isServer = NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;
            if (isServer && Vector3.Angle(pivot.forward, toEnemy) <= AlignToleranceDeg)
                TryFireAtEnemy(turret, enemy);
        }

        /// <summary>
        /// Gentle left-right sweep around the facing captured when the turret was
        /// hijacked, so an allied turret with no target looks alive instead of frozen.
        /// NOTE: this rotates `centerPoint`. If the turret model does NOT visibly turn
        /// in-game, centerPoint is not the rotation pivot — the one-time hierarchy log
        /// (see LogHierarchyOnce) tells us which child transform to rotate instead.
        /// </summary>
        private static void IdleScan(Turret turret, Transform pivot)
        {
            int id = turret.GetInstanceID();
            Quaternion baseRot = _baseRotation.TryGetValue(id, out var b) ? b : pivot.rotation;
            float yaw = Mathf.Sin(Time.time * 0.6f) * 50f;     // +/- 50 degrees sweep
            Quaternion target = baseRot * Quaternion.Euler(0f, yaw, 0f);
            pivot.rotation = Quaternion.Slerp(pivot.rotation, target, Time.deltaTime * 2.5f);
        }

        /// <summary>
        /// One-time diagnostic: dumps the turret's child transforms (and which carry a
        /// Renderer / Light / LineRenderer) so we can identify the exact node the game
        /// rotates, and drive that instead of centerPoint if needed.
        /// </summary>
        private static void LogHierarchyOnce(Turret turret)
        {
            int id = turret.GetInstanceID();
            if (!_loggedHierarchy.Add(id)) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[Turret hierarchy {id}] (R=Renderer L=Light Ln=LineRenderer)");
            foreach (var t in turret.GetComponentsInChildren<Transform>(true))
            {
                string tags = "";
                if (t.GetComponent<Renderer>() != null) tags += "R";
                if (t.GetComponent<Light>() != null) tags += "L";
                if (t.GetComponent<LineRenderer>() != null) tags += "Ln";
                string centre = (turret.centerPoint == t) ? "  <== centerPoint" : "";
                sb.AppendLine($"  {t.name} [{tags}]{centre}");
            }
            Plugin.Log.LogInfo(sb.ToString());
        }

        /// <summary>
        /// Draw the allied beam from the barrel along its forward axis, stopping at the
        /// first surface it hits (or at detection range). Gives a clear green laser
        /// while the turret is on our side.
        /// </summary>
        private static void UpdateBeam(Turret turret, Transform pivot)
        {
            float range = ModConfig.EnemyDetectionRange.Value;
            Vector3 start = pivot.position + pivot.forward * 0.5f;
            Vector3 end = Physics.Raycast(start, pivot.forward, out RaycastHit hit, range, ~0,
                                          QueryTriggerInteraction.Ignore)
                ? hit.point
                : start + pivot.forward * range;

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
        private void DriveManually(Turret turret, Transform pivot, ControlInfo ctrl)
        {
            if (ctrl.AimDirection.sqrMagnitude > 0.001f)
                pivot.rotation = Quaternion.LookRotation(ctrl.AimDirection.normalized);

            // Always show the green beam while manually aiming.
            UpdateBeam(turret, pivot);

            bool isServer = NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;
            if (ctrl.Firing && isServer)
                ManualFire(turret, pivot);
        }

        private void ManualFire(Turret turret, Transform pivot)
        {
            int id = turret.GetInstanceID();
            float now = Time.time;
            if (_nextFire.TryGetValue(id, out float next) && now < next) return;
            _nextFire[id] = now + FireInterval;

            int damage = ModConfig.ManualControlDamage.Value;
            Vector3 origin = pivot.position + pivot.forward * 0.5f;

            // Hit the first thing in the line of fire (broad mask; we filter by what we hit).
            if (Physics.Raycast(origin, pivot.forward, out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore))
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

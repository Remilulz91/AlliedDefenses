using System;
using AlliedDefenses.Config;
using AlliedDefenses.Core;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace AlliedDefenses.Defenses
{
    /// <summary>
    /// "Mine" (Landmine) module.
    ///
    /// Vanilla behavior: a landmine arms when a PLAYER enters its trigger
    /// (OnTriggerEnter) and explodes when the player leaves it (OnTriggerExit ->
    /// TriggerMineOnLocalClientByExiting). It never reacts to monsters.
    ///
    /// Allied behavior (this module):
    ///   - Players are completely safe: MinePatches blocks the vanilla player
    ///     trigger while the mine is allied (no friendly fire).
    ///   - Enemies become the trigger: every frame (host only) we look for a living
    ///     enemy within MineTriggerRadius and, if found, detonate the mine using the
    ///     game's own networked explosion path, killing whatever is on top of it.
    ///
    /// Like turrets, mines DO have a terminal code (e.g. "U9"): in vanilla you can
    /// type it to disable the mine for ~2 seconds (it turns green -> red -> green).
    /// So mines can be hijacked individually by id ("ally U9") via the shared
    /// TerminalCodeResolver, or all at once as a group ("ally mines").
    ///
    /// Confirmed vanilla members (verified via the LandminesForAll mod source):
    ///   - bool hasExploded
    ///   - void PressMineServerRpc()
    ///   - void TriggerMineOnLocalClientByExiting()  (private)
    ///   - OnTriggerEnter(Collider) / OnTriggerExit(Collider)
    /// </summary>
    public class MineHijack : IHijackableDefense
    {
        public string TypeId => "mine";
        public string DisplayName => "Mine";
        public Type ComponentType => typeof(Landmine);

        // Mines have a terminal code just like turrets -> resolve by id.
        public bool TryResolveByTerminalCode(string code, out Component? defense)
        {
            defense = TerminalCodeResolver.Resolve(code, typeof(Landmine));
            return defense != null;
        }

        public void ApplyAlliedState(Component defense, bool allied)
        {
            // Player-safety is handled by MinePatches and enemy detonation by
            // TickAlliedTargeting. We only restore the light colour when it stops
            // being allied (the green tint is (re)applied each tick below).
            if (!allied)
                AlliedLightTint.Apply(defense, false);
        }

        public void TickAlliedTargeting(Component defense)
        {
            if (defense is not Landmine mine || mine.hasExploded) return;

            // Keep the mine's light green so you can SEE it's hijacked (re-asserted each
            // frame in case the game animates it back to red).
            AlliedLightTint.Apply(mine, true);

            // Only the host decides to detonate; the explosion is then networked to
            // everyone by the game's own RPC, so it stays in sync.
            if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer) return;

            var enemy = TargetingHelper.FindBestEnemy(
                mine.transform.position, Vector3.up,
                range: ModConfig.MineTriggerRadius.Value,
                coneHalfAngle: 180f,
                requireLineOfSight: false); // a mine doesn't need line of sight to its target

            if (enemy == null) return;

            Detonate(mine);
        }

        /// <summary>
        /// Sets off the mine through the game's own networked explosion method, the
        /// same one used when a player steps off a live mine.
        /// </summary>
        private static void Detonate(Landmine mine)
        {
            if (mine.hasExploded) return;
            try
            {
                // TriggerMineOnLocalClientByExiting() is private but drives the full
                // networked explosion (ExplodeMineServerRpc -> ClientRpc internally).
                Traverse.Create(mine).Method("TriggerMineOnLocalClientByExiting").GetValue();
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning($"Failed to detonate allied mine: {e.Message}");
            }
        }
    }
}

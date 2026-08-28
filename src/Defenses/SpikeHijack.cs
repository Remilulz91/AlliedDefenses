using System;
using AlliedDefenses.Core;
using UnityEngine;

namespace AlliedDefenses.Defenses
{
    /// <summary>
    /// "Spike trap" (ceiling spike / SpikeRoofTrap) module.
    ///
    /// Vanilla behavior: the spike trap slams down and crushes whatever is under it —
    /// PLAYERS and killable enemies alike. Like turrets and mines it has a terminal code
    /// (shown on the radar), so it's resolvable by id.
    ///
    /// Allied behavior: players become safe. SpikePatches suppresses the vanilla damage
    /// to players while the trap is allied; enemies caught under it are still crushed as
    /// usual. (A future version could also make an allied spike actively slam when an
    /// enemy is underneath.)
    ///
    /// Confirmed: the damage happens in SpikeRoofTrap.OnTriggerStay(Collider) — that's
    /// where SpikePatches hooks.
    /// </summary>
    public class SpikeHijack : IHijackableDefense
    {
        public string TypeId => "spike";
        public string DisplayName => "Spike trap";
        public Type ComponentType => typeof(SpikeRoofTrap);

        public bool TryResolveByTerminalCode(string code, out Component? defense)
        {
            defense = TerminalCodeResolver.Resolve(code, typeof(SpikeRoofTrap));
            return defense != null;
        }

        public void ApplyAlliedState(Component defense, bool allied)
        {
            // Player-safety is handled by SpikePatches. Tint any indicator light green
            // (no-op if the trap has no light).
            AlliedLightTint.Apply(defense, allied);
        }

        // The trap slams on its own; nothing to drive each frame (v1).
        public void TickAlliedTargeting(Component defense) { }
    }
}

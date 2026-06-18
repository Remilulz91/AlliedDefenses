using UnityEngine;

namespace AlliedDefenses.Core
{
    /// <summary>
    /// Common contract for EVERY hijackable defense (turret, mine, and anything you
    /// add later). This is the heart of the extensible design: to support a new
    /// "weapon", just create a class that implements this interface and register it
    /// in DefenseRegistry.
    ///
    /// The rest of the mod (terminal, networking, manager) only ever knows about this
    /// interface: it does not care whether it is dealing with a turret or a mine, so
    /// there are no "if (it's a turret) ... else if (it's a mine) ..." chains anywhere.
    /// </summary>
    public interface IHijackableDefense
    {
        /// <summary>Unique internal type id, e.g. "turret", "mine".</summary>
        string TypeId { get; }

        /// <summary>Player-facing name, e.g. "Turret", "Mine".</summary>
        string DisplayName { get; }

        /// <summary>
        /// The Unity component type from the game that this module drives
        /// (e.g. typeof(Turret)). Used to find the right component on a network
        /// object when syncing state across machines.
        /// </summary>
        System.Type ComponentType { get; }

        /// <summary>
        /// Find an instance of this defense from the code shown in the terminal
        /// (e.g. "A0"). Returns false if there is no match.
        /// </summary>
        bool TryResolveByTerminalCode(string code, out Component? defense);

        /// <summary>
        /// Enable / disable "allied" mode on a specific instance. This is where we
        /// set the state the Harmony patches will read to change targeting. Does NOT
        /// handle networking: syncing is done upstream by HijackManager + the networker.
        /// </summary>
        void ApplyAlliedState(Component defense, bool allied);

        /// <summary>
        /// Targeting logic run regularly while the defense is allied. For the turret
        /// most of the work happens through Harmony patches on its own loop; this
        /// method is for defenses (like the mine) that have no native aiming loop and
        /// must be driven by us. Can be left empty.
        /// </summary>
        void TickAlliedTargeting(Component defense);
    }
}

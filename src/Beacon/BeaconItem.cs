using UnityEngine;
using AlliedDefenses.Core;

namespace AlliedDefenses.Beacon
{
    /// <summary>
    /// The carryable "Defense Beacon": a two-handed heavy prop (a <see cref="GrabbableObject"/>)
    /// you buy once, haul out of the ship, and set down anywhere in the field. Wherever it is —
    /// held or on the ground — it registers its position in <see cref="BeaconRegistry"/>, which is
    /// the anchor the protective auras key off (Ghost Girl sanity drain, sand-worm untargetable,
    /// Eyeless-Dog muffle). So dropping it near the ship or out by the worm creates a safe bubble.
    ///
    /// It carries no battery and has no "use" action — it is inert on purpose (the balance choice
    /// from the design: it hides/deters, it does not shoot). We only override Start/OnDestroy to
    /// register and unregister with the beacon directory; everything else is vanilla grab/drop
    /// physics handled by the base class, which the game already networks.
    /// </summary>
    public class BeaconItem : GrabbableObject
    {
        private bool _registered;

        public override void Start()
        {
            base.Start();
            Register();
        }

        // Belt-and-suspenders: if the object is re-enabled after a pool/scene move, make sure
        // it is in the directory. Register() is idempotent (the registry ignores duplicates).
        private void OnEnable() => Register();

        private void Register()
        {
            if (_registered) return;
            BeaconRegistry.Register(transform);
            _registered = true;
        }

        public override void OnDestroy()
        {
            BeaconRegistry.Unregister(transform);
            _registered = false;
            base.OnDestroy();
        }
    }
}

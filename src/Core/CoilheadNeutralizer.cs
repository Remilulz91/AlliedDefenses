using System.Collections.Generic;
using UnityEngine;

namespace AlliedDefenses.Core
{
    /// <summary>
    /// Coil-Head (SpringManAI) counter-play. A Coil-Head cannot be killed and is immune to
    /// stun grenades, but it only moves while no player is looking at it. An allied turret
    /// that watches it acts like that "gaze": while the turret aims at a Coil-Head we mark
    /// it neutralized, and <see cref="Patches.SpringManFreezePatch"/> forces its NavMeshAgent
    /// to a halt after the game's own Update, keeping it frozen in place.
    ///
    /// The mark carries a short expiry that the turret refreshes every frame it holds sight,
    /// so the freeze lingers briefly after the turret loses the target (linger length scales
    /// with the 'neutralize' upgrade). Gated behind that upgrade (level 0 = off).
    /// </summary>
    public static class CoilheadNeutralizer
    {
        // SpringManAI -> Time.time until which it stays frozen.
        private static readonly Dictionary<SpringManAI, float> _until = new();

        /// <summary>Mark a Coil-Head as neutralized for at least <paramref name="linger"/> seconds.</summary>
        public static void Neutralize(SpringManAI coil, float linger)
        {
            if (coil == null) return;
            _until[coil] = Time.time + Mathf.Max(0.1f, linger);
        }

        /// <summary>Is this Coil-Head currently frozen by an allied turret?</summary>
        public static bool IsNeutralized(SpringManAI coil)
        {
            if (coil == null) return false;
            if (!_until.TryGetValue(coil, out float t)) return false;
            if (Time.time >= t) { _until.Remove(coil); return false; }
            return true;
        }
    }
}

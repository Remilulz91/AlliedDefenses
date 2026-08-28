using HarmonyLib;
using UnityEngine;
using AlliedDefenses.Core;

namespace AlliedDefenses.Patches
{
    /// <summary>
    /// Eyeless Dog (MouthDogAI) counter-play — "sound muffle".
    ///
    /// Eyeless Dogs are blind and hunt purely by hearing: every noise the game emits is fed to
    /// MouthDogAI.DetectNoise(noisePosition, loudness, ...), which raises the dog's suspicion and
    /// sends it toward the sound. This prefix drops any noise whose ORIGIN lies inside an allied
    /// defense radius (a placed beacon around the ship) — so a player standing in the beacon's
    /// "quiet zone" is effectively silent to the dogs. It does not kill them; it hides you.
    ///
    /// Returning false skips DetectNoise entirely, so the dog never reacts to that noise. Purely
    /// positional (we never need to know which player made it), and gated behind the 'muffle'
    /// upgrade (level 0 = off). Wrapped in try/catch so it can never crash the enemy update.
    /// </summary>
    [HarmonyPatch(typeof(MouthDogAI), "DetectNoise")]
    internal static class MouthDogMufflePatch
    {
        [HarmonyPrefix]
        private static bool MuffleNoiseInQuietZone(Vector3 noisePosition)
        {
            try
            {
                if (!UpgradeManager.MuffleEnabled) return true; // upgrade off -> vanilla hearing
                float radius = UpgradeManager.MuffleRadius();
                if (radius <= 0f) return true;
                if (HijackManager.AnyAlliedWithin(noisePosition, radius))
                    return false; // noise happened in the quiet zone -> the dog never hears it
            }
            catch { /* never let counter-play break the dog */ }
            return true;
        }
    }
}

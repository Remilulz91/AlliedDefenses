using System;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;
using AlliedDefenses.Config;
using AlliedDefenses.Core;

namespace AlliedDefenses.Beacon
{
    /// <summary>
    /// Owns the Defense Beacon lifecycle: registering the prefab, the one-time purchase (which
    /// reuses the 'beacon' pseudo-upgrade so persistence/reset are shared), and delivering a
    /// beacon into the ship. The beacon is NOT saved through the vanilla item-save system (custom
    /// runtime items are fragile there); instead the HOST re-delivers one each session while it is
    /// owned, and a lost beacon can be re-delivered for free. That keeps "buy once, never repay"
    /// true without risking save corruption.
    /// </summary>
    [HarmonyPatch]
    public static class BeaconManager
    {
        // Build + register the network prefab once the NetworkManager exists.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameNetworkManager), "Start")]
        public static void OnGameNetworkStart()
        {
            if (!ModConfig.EnableBeacon.Value) return;
            BeaconFactory.EnsureBuilt();
        }

        // Deliver the owned beacon at session start (host only). Clients receive the replica.
        [HarmonyPostfix]
        [HarmonyPatch(typeof(StartOfRound), "Start")]
        public static void OnRoundStart()
        {
            if (!ModConfig.EnableBeacon.Value) return;
            if (!UpgradeManager.BeaconOwned) return;
            SpawnIfMissing();
        }

        /// <summary>
        /// Terminal "ally beacon": buy it the first time (paying), or re-deliver a missing one for
        /// free afterwards. Returns (message, creditsSpent) — the caller deducts the credits.
        /// </summary>
        public static (string message, int spent) Purchase(int credits)
        {
            if (!ModConfig.EnableBeacon.Value)
                return ("The Defense Beacon is disabled in the config.", 0);
            if (!ModConfig.EnableUpgrades.Value)
                return ("Upgrades/economy are disabled in the config, so the beacon cannot be bought.", 0);

            if (UpgradeManager.BeaconOwned)
            {
                bool delivered = SpawnIfMissing();
                return (delivered
                    ? "Defense Beacon re-delivered to the ship (free). Carry it out with two hands and set it down."
                    : "You already own the Defense Beacon - it is in the ship or out in the field.", 0);
            }

            var (msg, spent) = UpgradeManager.Buy("beacon", credits);
            if (spent > 0)
            {
                SpawnIfMissing();
                msg = $"Purchased the Defense Beacon (-{spent} credits). Delivered to the ship: carry it " +
                      "out with two hands and set it down to protect that spot. Upgrade its auras with " +
                      "'sanity', 'seismic', 'muffle', and 'haul' (carry weight).";
            }
            return (msg, spent);
        }

        /// <summary>True if a beacon currently exists anywhere (host authority check).</summary>
        private static bool BeaconExists() => UnityEngine.Object.FindObjectOfType<BeaconItem>() != null;

        /// <summary>Host spawns one beacon in the ship if none exists. Returns true if it spawned one.</summary>
        public static bool SpawnIfMissing()
        {
            try
            {
                var nm = NetworkManager.Singleton;
                if (nm == null || !(nm.IsHost || nm.IsServer)) return false; // only the host spawns
                if (BeaconExists()) return false;

                BeaconFactory.EnsureBuilt();
                if (BeaconFactory.Prefab == null)
                {
                    Plugin.Log.LogError("BeaconManager: prefab missing; cannot deliver beacon.");
                    return false;
                }

                Vector3 pos = ShipSpawnPoint();
                var go = UnityEngine.Object.Instantiate(BeaconFactory.Prefab, pos, Quaternion.identity);
                go.hideFlags = HideFlags.None;

                var grab = go.GetComponent<BeaconItem>();
                if (grab != null)
                {
                    // Refresh weight to the current 'haul' level, then let the item settle to the floor.
                    if (grab.itemProperties != null) grab.itemProperties.weight = UpgradeManager.BeaconWeight();
                    grab.fallTime = 0f;
                    grab.hasHitGround = false;
                    grab.reachedFloorTarget = false;
                    grab.targetFloorPosition = pos;
                }

                go.GetComponent<NetworkObject>().Spawn(destroyWithScene: false);
                Plugin.Log.LogInfo("BeaconManager: Defense Beacon delivered to the ship.");
                return true;
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"BeaconManager: failed to deliver beacon: {e}");
                return false;
            }
        }

        /// <summary>A safe spot inside the ship to drop the beacon.</summary>
        private static Vector3 ShipSpawnPoint()
        {
            try
            {
                var sor = StartOfRound.Instance;
                if (sor != null)
                {
                    // Prefer the ship's interior anchor; fall back to the elevator (ship) transform.
                    var mid = Traverse.Create(sor).Field("middleOfShipNode").GetValue<Transform>();
                    if (mid != null) return mid.position + Vector3.up * 0.5f;
                    if (sor.elevatorTransform != null)
                        return sor.elevatorTransform.position + Vector3.up * 0.5f + sor.elevatorTransform.forward * 1.5f;
                }
            }
            catch { /* fall through */ }
            return new Vector3(0f, 1f, 0f);
        }
    }
}

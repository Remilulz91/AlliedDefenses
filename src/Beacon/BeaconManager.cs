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

        /// <summary>
        /// Host despawns every beacon in the world (network-wide). Called when beacon ownership is
        /// removed (e.g. 'ally upgrade reset'), so the physical lamp doesn't linger after a reset.
        /// </summary>
        public static void DespawnAll()
        {
            try
            {
                var nm = NetworkManager.Singleton;
                if (nm == null || !(nm.IsHost || nm.IsServer)) return; // only the host despawns

                var beacons = UnityEngine.Object.FindObjectsOfType<BeaconItem>();
                foreach (var b in beacons)
                {
                    var netObj = b.GetComponent<NetworkObject>();
                    if (netObj != null && netObj.IsSpawned) netObj.Despawn(destroy: true);
                    else UnityEngine.Object.Destroy(b.gameObject);
                }
                if (beacons.Length > 0)
                    Plugin.Log.LogInfo($"BeaconManager: despawned {beacons.Length} beacon(s) (ownership reset).");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"BeaconManager: despawn failed: {e}");
            }
        }

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
                go.SetActive(true); // the template is kept inactive; the live copy must be active

                var grab = go.GetComponent<BeaconItem>();
                if (grab != null)
                {
                    if (grab.itemProperties != null) grab.itemProperties.weight = UpgradeManager.BeaconWeight();

                    // Rest it exactly on the floor below the spawn point. Raycast down, ignoring the
                    // beacon's own layer (6), then mark it as already settled so it does not run the
                    // falling animation (which needs clear conditions and could misbehave).
                    Vector3 floor = pos;
                    if (Physics.Raycast(pos + Vector3.up * 1f, Vector3.down, out var hit, 8f,
                                        ~(1 << 6), QueryTriggerInteraction.Ignore))
                        floor = hit.point;

                    // The beacon's pivot is at its centre, so lift it half its height off the floor
                    // (matches itemProperties.verticalOffset) — a floor-level pivot breaks the grab.
                    Vector3 rest = floor + Vector3.up * 0.5f;
                    go.transform.position = rest;
                    grab.fallTime = 1f;
                    grab.hasHitGround = true;
                    grab.reachedFloorTarget = true;
                    grab.targetFloorPosition = rest;
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

        /// <summary>
        /// A clear, floor-level spot inside the ship to drop the beacon. This matters: the grab
        /// only works if the game can trace an unobstructed line from the camera to the item, so
        /// the beacon must rest on open floor (not floating up near a shelf, which was the bug).
        /// We use a player spawn point (always on the ship floor in the open).
        /// </summary>
        private static Vector3 ShipSpawnPoint()
        {
            try
            {
                var sor = StartOfRound.Instance;
                if (sor != null)
                {
                    var spawns = sor.playerSpawnPositions;
                    if (spawns != null && spawns.Length > 0 && spawns[0] != null)
                        return spawns[0].position + Vector3.up * 0.2f;
                    if (sor.elevatorTransform != null)
                        return sor.elevatorTransform.position + Vector3.up * 0.2f;
                }
            }
            catch { /* fall through */ }
            return new Vector3(0f, 1f, 0f);
        }
    }
}

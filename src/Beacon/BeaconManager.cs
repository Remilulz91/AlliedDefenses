using System;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;
using AlliedDefenses.Config;
using AlliedDefenses.Core;

namespace AlliedDefenses.Beacon
{
    /// <summary>
    /// Owns the Defense Beacon lifecycle: registering the network prefab, the one-time purchase
    /// (reusing the 'beacon' pseudo-upgrade), and DEPLOYING / RECALLING the beacon at a position.
    ///
    /// The beacon is no longer carried. You buy it once, then deploy it at your feet with a key
    /// (see BeaconDeployInput); the host spawns a bare-NetworkObject beacon there (reliable in MP).
    /// Deploying again moves it (old one despawned first). 'ally upgrade reset' removes it.
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

        /// <summary>
        /// Terminal "ally beacon": buy it the first time (paying). No physical delivery here — the
        /// owner deploys it with the deploy key. Returns (message, creditsSpent).
        /// </summary>
        public static (string message, int spent) Purchase(int credits)
        {
            if (!ModConfig.EnableBeacon.Value)
                return ("The Defense Beacon is disabled in the config.", 0);
            if (!ModConfig.EnableUpgrades.Value)
                return ("Upgrades/economy are disabled in the config, so the beacon cannot be bought.", 0);

            string key = ModConfig.BeaconDeployKey.Value;

            if (UpgradeManager.BeaconOwned)
                return ($"You already own the Defense Beacon. Press [{key}] to deploy/move it to where you stand.", 0);

            var (msg, spent) = UpgradeManager.Buy("beacon", credits);
            if (spent > 0)
                msg = $"Purchased the Defense Beacon (-{spent} credits). Press [{key}] where you want protection " +
                      "to deploy it (press again to move it). Upgrade its auras with 'sanity', 'seismic', " +
                      "'muffle', 'barber', 'slime', 'bees'.";
            return (msg, spent);
        }

        /// <summary>True if a deployed beacon currently exists (host authority check).</summary>
        private static bool BeaconExists() => UnityEngine.Object.FindObjectOfType<BeaconObject>() != null;

        /// <summary>
        /// Host deploys the beacon at a world position (moving it if one already exists). Called on
        /// the host directly, or from a client's request RPC.
        /// </summary>
        public static void DeployAt(Vector3 worldPos)
        {
            try
            {
                var nm = NetworkManager.Singleton;
                if (nm == null || !(nm.IsHost || nm.IsServer)) return; // only the host spawns
                if (!UpgradeManager.BeaconOwned) return;

                DespawnAll();            // move = despawn old, spawn new
                BeaconFactory.EnsureBuilt();
                if (BeaconFactory.Prefab == null)
                {
                    Plugin.Log.LogError("BeaconManager: prefab missing; cannot deploy beacon.");
                    return;
                }

                // Rest it on the floor at the requested spot: raycast down, lift the pivot half the
                // model height so the model sits on the ground (matches BeaconObject's ring offset).
                Vector3 floor = worldPos;
                if (Physics.Raycast(worldPos + Vector3.up * 1.5f, Vector3.down, out var hit, 6f, ~0,
                                    QueryTriggerInteraction.Ignore))
                    floor = hit.point;
                Vector3 rest = floor + Vector3.up * 0.5f;

                var go = UnityEngine.Object.Instantiate(BeaconFactory.Prefab, rest, Quaternion.identity);
                go.hideFlags = HideFlags.None;
                go.SetActive(true);
                go.GetComponent<NetworkObject>().Spawn(destroyWithScene: false);
                Plugin.Log.LogInfo("BeaconManager: Defense Beacon deployed.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"BeaconManager: deploy failed: {e}");
            }
        }

        /// <summary>Host despawns every deployed beacon (network-wide). Used by recall and reset.</summary>
        public static void DespawnAll()
        {
            try
            {
                var nm = NetworkManager.Singleton;
                if (nm == null || !(nm.IsHost || nm.IsServer)) return;

                var beacons = UnityEngine.Object.FindObjectsOfType<BeaconObject>();
                foreach (var b in beacons)
                {
                    var netObj = b.GetComponent<NetworkObject>();
                    if (netObj != null && netObj.IsSpawned) netObj.Despawn(destroy: true);
                    else UnityEngine.Object.Destroy(b.gameObject);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"BeaconManager: despawn failed: {e}");
            }
        }
    }
}

using System;
using System.Reflection;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace AlliedDefenses.Networking
{
    /// <summary>
    /// Creates, registers and spawns the mod's network object (the one carrying
    /// HijackNetworker). This is the required path to have custom RPCs in a Netcode
    /// for GameObjects game.
    ///
    ///   1) GameNetworkManager.Start  -> build the prefab {NetworkObject + HijackNetworker}
    ///      and register it as a known network prefab.
    ///   2) StartOfRound.Start        -> the HOST instantiates and spawns it (once), so
    ///      it is replicated to all clients. We spawn in Start (not Awake) and guard on
    ///      HijackNetworker.Instance to be sure the host/server is ready.
    ///
    /// Every step logs, and risky calls are wrapped in try/catch, so the BepInEx log
    /// tells you exactly what happened.
    /// </summary>
    // The class-level [HarmonyPatch] is REQUIRED: Harmony's PatchAll() only scans
    // classes that carry it. Without it, the method-level patches below are silently
    // ignored (this was the bug that left the network handler unspawned). The bare
    // attribute is enough; each method declares its own target type.
    [HarmonyPatch]
    public static class NetworkObjectManager
    {
        private static GameObject? _networkPrefab;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameNetworkManager), "Start")]
        public static void RegisterPrefab()
        {
            Plugin.Log.LogInfo("NetworkObjectManager: GameNetworkManager.Start reached.");

            if (_networkPrefab != null)
            {
                Plugin.Log.LogInfo("NetworkObjectManager: prefab already created, skipping.");
                return;
            }

            try
            {
                if (NetworkManager.Singleton == null)
                {
                    Plugin.Log.LogError("NetworkObjectManager: NetworkManager.Singleton is null; cannot register prefab.");
                    return;
                }

                _networkPrefab = new GameObject("AlliedDefensesNetworkHandler");
                UnityEngine.Object.DontDestroyOnLoad(_networkPrefab);
                _networkPrefab.hideFlags = HideFlags.HideAndDontSave;

                var netObj = _networkPrefab.AddComponent<NetworkObject>();
                _networkPrefab.AddComponent<HijackNetworker>();

                AssignStableHash(netObj, "AlliedDefenses.HijackNetworker");

                NetworkManager.Singleton.AddNetworkPrefab(_networkPrefab);
                Plugin.Log.LogInfo("NetworkObjectManager: network prefab registered.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"NetworkObjectManager: failed to register prefab: {e}");
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(StartOfRound), "Start")]
        public static void SpawnHandler()
        {
            try
            {
                var nm = NetworkManager.Singleton;
                if (nm == null)
                {
                    Plugin.Log.LogWarning("NetworkObjectManager: NetworkManager null at StartOfRound.Start.");
                    return;
                }
                if (!(nm.IsHost || nm.IsServer))
                {
                    Plugin.Log.LogInfo("NetworkObjectManager: not the host; client will receive the handler from the host.");
                    return;
                }
                if (HijackNetworker.Instance != null)
                {
                    Plugin.Log.LogInfo("NetworkObjectManager: handler already spawned.");
                    return;
                }
                if (_networkPrefab == null)
                {
                    Plugin.Log.LogError("NetworkObjectManager: prefab is null (registration failed?); cannot spawn.");
                    return;
                }

                var instance = UnityEngine.Object.Instantiate(_networkPrefab);
                instance.GetComponent<NetworkObject>().Spawn(destroyWithScene: false);
                Plugin.Log.LogInfo("NetworkObjectManager: HijackNetworker spawned by the host.");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError($"NetworkObjectManager: failed to spawn handler: {e}");
            }
        }

        private static void AssignStableHash(NetworkObject netObj, string key)
        {
            uint hash = (uint)key.GetHashCode();
            var field = typeof(NetworkObject).GetField(
                "GlobalObjectIdHash",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null)
            {
                Plugin.Log.LogError("NetworkObjectManager: GlobalObjectIdHash field not found; spawn will likely fail.");
                return;
            }
            field.SetValue(netObj, hash);
            Plugin.Log.LogInfo($"NetworkObjectManager: GlobalObjectIdHash set to {hash}.");
        }
    }
}

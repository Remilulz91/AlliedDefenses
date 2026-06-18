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
    /// Two steps, following the standard modding-wiki pattern:
    ///   1) When the network manager starts (GameNetworkManager.Start), we build a
    ///      prefab {NetworkObject + HijackNetworker} and register it as a known
    ///      "network prefab".
    ///   2) When a game starts (StartOfRound.Awake), the HOST instantiates and spawns
    ///      that object; it is then replicated to all clients.
    /// </summary>
    public static class NetworkObjectManager
    {
        private static GameObject? _networkPrefab;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameNetworkManager), "Start")]
        public static void RegisterPrefab()
        {
            if (_networkPrefab != null) return;

            _networkPrefab = new GameObject("AlliedDefensesNetworkHandler");
            var netObj = _networkPrefab.AddComponent<NetworkObject>();
            _networkPrefab.AddComponent<HijackNetworker>();

            // A NetworkObject needs a stable global hash so host and clients recognize
            // it the same way. We set one via reflection.
            AssignStableHash(netObj, "AlliedDefenses.HijackNetworker");

            NetworkManager.Singleton.AddNetworkPrefab(_networkPrefab);
            Plugin.Log.LogInfo("Network prefab registered.");
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(StartOfRound), "Awake")]
        public static void SpawnHandler()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !(nm.IsHost || nm.IsServer)) return; // only the host spawns
            if (_networkPrefab == null) return;

            var instance = Object.Instantiate(_networkPrefab);
            instance.GetComponent<NetworkObject>().Spawn(destroyWithScene: false);
            Plugin.Log.LogInfo("HijackNetworker spawned by the host.");
        }

        private static void AssignStableHash(NetworkObject netObj, string key)
        {
            uint hash = (uint)key.GetHashCode();
            // GlobalObjectIdHash is read-only publicly -> set it via reflection.
            var field = typeof(NetworkObject).GetField(
                "GlobalObjectIdHash",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            field?.SetValue(netObj, hash);
        }
    }
}

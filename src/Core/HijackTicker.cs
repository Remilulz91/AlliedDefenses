using UnityEngine;

namespace AlliedDefenses.Core
{
    /// <summary>
    /// A tiny invisible component, created once at startup, whose only job is to call
    /// HijackManager.Tick() every frame (hijack expiry + targeting of passive defenses
    /// like mines).
    ///
    /// We use a dedicated MonoBehaviour rather than patching one of the game's Update()
    /// loops: it is more robust (no game method name to guess) and survives scene
    /// changes thanks to DontDestroyOnLoad.
    /// </summary>
    public class HijackTicker : MonoBehaviour
    {
        public static HijackTicker? Instance { get; private set; }

        public static void EnsureExists()
        {
            if (Instance != null) return;
            var go = new GameObject("AlliedDefenses_Ticker");
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            Instance = go.AddComponent<HijackTicker>();

            // Local-player input for manual turret control lives on the same object.
            go.AddComponent<ManualControlInput>();
        }

        private void Update() => HijackManager.Tick();
    }
}

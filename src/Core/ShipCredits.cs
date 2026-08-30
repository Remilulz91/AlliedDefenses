using HarmonyLib;
using UnityEngine;

namespace AlliedDefenses.Core
{
    /// <summary>
    /// Reads/writes the ship's shared credits (Terminal.groupCredits) via reflection, so it stays
    /// build-safe against member renames. Writing also fires the game's own credit-sync RPC — but
    /// that RPC requires terminal ownership, so it only actually syncs when called on the HOST.
    /// Client purchases must therefore be routed to the host (see HijackNetworker.RequestPurchase).
    /// </summary>
    public static class ShipCredits
    {
        public static Terminal Find() => UnityEngine.Object.FindObjectOfType<Terminal>();

        public static int Get(Terminal terminal)
        {
            if (terminal == null) return 0;
            try { return Traverse.Create(terminal).Field("groupCredits").GetValue<int>(); }
            catch { return 0; }
        }

        public static void Set(Terminal terminal, int value)
        {
            if (terminal == null) return;
            value = Mathf.Max(0, value);
            try
            {
                Traverse.Create(terminal).Field("groupCredits").SetValue(value);
                int items = 0;
                try { items = Traverse.Create(terminal).Field("numberOfItemsInDropship").GetValue<int>(); } catch { }
                // Host-only effective: syncs the new balance to everyone.
                try { Traverse.Create(terminal).Method("SyncGroupCreditsServerRpc", value, items).GetValue(); } catch { }
            }
            catch (System.Exception e) { Plugin.Log.LogWarning($"ShipCredits.Set failed: {e.Message}"); }
        }
    }
}

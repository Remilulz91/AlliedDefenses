# Guide — Carryable Defense Beacon via a Unity AssetBundle (advanced)

The current in-game beacon is a **deploy-in-place** object (reliable in multiplayer). If you want the
original **carryable two-handed** beacon back — done the *robust* way — the item has to be built as a
real prefab in the **Unity Editor** and shipped as an **AssetBundle**, instead of assembled from
primitives at runtime. That's the only reliable way to have a custom *networked grabbable* item in
Lethal Company, because Netcode's NetworkObject/RPC wiring must come from a proper prefab, not from
`AddComponent` at runtime.

This guide is for whoever does the 3D/Unity side. Once you have a working bundle, the mod author
wires it in (see "Handing it back" at the end).

---

## 1. Install the exact tooling

- **Unity Hub** + **Unity Editor `2022.3.62f1`** (must match the game's Unity version for LC v81; if
  the game updates, match the new version — check the game's `UnityPlayer` / a mod that prints it).
- The **HDRP** render pipeline package (Lethal Company uses HDRP) and **Netcode for GameObjects**
  (`com.unity.netcode.gameobjects`) — versions matching the game (Netcode 1.5.2 / 1.12.2-ish; use
  what the game ships).

The easiest path is the community **Lethal Company Unity template project**, which already has the
right Unity version, packages, and the game's stripped assemblies referenced. Start from the
official modding wiki:

- **https://lethal.wiki/** → "Custom Content" → **"Making a custom item"** and **"AssetBundles"**.
  Follow that tutorial end to end — it is the canonical, maintained reference. This guide only adds
  the beacon-specific bits.

---

## 2. Build the beacon prefab (in the Editor)

Create a prefab that mirrors a vanilla grabbable item. The simplest is to **duplicate an existing
item prefab** from the game's asset ripper output (e.g. the Flashlight or Fancy Lamp) and swap the
mesh + script. A prefab from scratch needs, on the ROOT:

- `NetworkObject` (Netcode). Leave **Auto Object Parent Sync = OFF** (the game reparents grabbed
  items itself; auto-sync throws "Only the server can reparent" on clients).
- A `Rigidbody` (used by the fall/physics), matching a vanilla item's settings.
- A **BoxCollider** (trigger) for the grab ray, tagged **`PhysicsProp`**, on the grabbable layer
  (mirror a vanilla item — layer index 6 "Props" in LC).
- Your mesh + `MeshRenderer` (the lamp model, or any model you make).
- A `ScanNodeProperties` child (optional, for the scanner tooltip).
- Your **grabbable script** (below).

Also create an **`Item` ScriptableObject** asset (Netcode/LC `Item`): set `itemName`, `twoHanded =
true`, `twoHandedAnimation = true`, `weight`, `grabbable = true`, `canBeGrabbedBeforeGameStart =
true`, `itemIcon` (a 128x128 sprite), and `spawnPrefab = ` your prefab.

Put the prefab, the Item asset and the icon into an **AssetBundle** (name it e.g. `alliedbeacon`).

---

## 3. The grabbable script (C#)

Compile this in a small Unity C# assembly referenced by the prefab (or in the mod and reference the
type from the bundle). It's a normal `GrabbableObject` subclass — this is the whole point of the
bundle: the Netcode weaver processes it at build time, so it works over the network.

```csharp
using UnityEngine;

namespace AlliedBeaconBundle
{
    // A minimal networked, carryable beacon. Because this lives in a proper prefab + AssetBundle,
    // Netcode wires it correctly (unlike a runtime-built one). It just registers/unregisters its
    // position so the mod's auras can key off it; the mod finds it by this component type.
    public class BeaconGrabbable : GrabbableObject
    {
        public override void Start()
        {
            base.Start();
            // The mod hooks this via reflection/type name; nothing else needed here.
        }
    }
}
```

Keep it tiny. All the grab/drop/two-handed behaviour comes from the base `GrabbableObject`. The mod
supplies the auras, ring, and ownership.

> Tip: if referencing `GrabbableObject` in the Editor is awkward, you can instead ship the prefab
> **without** a custom script and let the mod `AddComponent` a `GrabbableObject`-derived type it
> owns — but the reliable route is to bake the script into the prefab via the bundle.

---

## 4. Build the bundle

In the Editor: **Assets → Build AssetBundles** (or the template's build menu). You get a file like
`alliedbeacon` (+ a `.manifest`). Ship that file inside the mod's plugin folder.

---

## Handing it back to the mod

Give the mod author:
1. the built **`alliedbeacon`** bundle file,
2. the **exact type name** of your grabbable script (`AlliedBeaconBundle.BeaconGrabbable`),
3. the **asset names** inside the bundle (prefab name + Item asset name).

The mod then, at runtime:
- loads the bundle: `AssetBundle.LoadFromFile(Path.Combine(pluginDir, "alliedbeacon"))`,
- loads the `Item`: `bundle.LoadAsset<Item>("BeaconItem")`,
- registers the network prefab (`NetworkManager.AddNetworkPrefab(item.spawnPrefab)`) at
  `GameNetworkManager.Start`,
- registers the shop item (e.g. via **LethalLib** `Items.RegisterShopItem`) or reuses the existing
  `ally beacon` purchase to spawn/deliver `item.spawnPrefab`,
- keeps the same aura/ring/ownership logic already in the mod, keyed off your `BeaconGrabbable`
  component.

That's a modest amount of glue code on the mod side — much smaller and far more reliable than
building the networked grabbable at runtime.

---

## Reality check

This is a genuine mini-project (a few hours if you're new to Unity/AssetBundles). The deploy-in-place
beacon that ships now already gives the full gameplay (buy, deploy anywhere, auras, rings) reliably
in multiplayer — the bundle route is purely to get the **carry-it-two-handed** feel back. Do it when
you have the time and appetite for the Unity side. Questions welcome via the repo issues.

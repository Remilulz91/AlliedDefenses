# Guide — Physical "Mini Terminal" item via Unity + AssetBundle (for a beginner)

**For the friend who offered to do the Unity side.** This gets you the *physical held object* version
of the Hack Tool: a custom item you buy, hold, and use inside the facility to trigger doors/turrets.

## Read this first (honest expectations)

- The mod already ships a working **Hack Tool** with **no Unity needed** (buy `ally hack`, aim, press
  a key). This guide is only to add the **physical held object** on top of it.
- **Unity is a visual editor**, so this can't be 100% copy-paste — some steps are "click here, drag
  this into that box". I give you every value and every script, plus exact click paths. Take it
  slowly; it's very doable even as a first Unity project.
- A **fully working on-screen typing terminal** (a real screen you type on) is *far* beyond a first
  project — we're NOT doing that. Realistic target: a **grabbable item that, when you use it (left
  click while holding), hacks whatever you're aiming at** (same effect as the key tool). That's the
  90% version and it's achievable.
- If you get stuck on any step, screenshot it and send it — the mod author can help.

---

## Step 1 — Install the tools (once)

1. Install **Unity Hub** (free): https://unity.com/download
2. In Unity Hub → *Installs* → *Install Editor* → pick **Unity 2022.3.62f1** (must match Lethal
   Company; if the game updates, match the new version). Include **"Windows Build Support (IL2CPP)"**
   is not needed — default modules are fine.
3. Download the community **Lethal Company Unity project template** (it has the right packages —
   HDRP, Netcode — and the game's assemblies referenced). Start here and follow "AssetBundles":
   - **https://lethal.wiki/** → *Custom Content* → *"Making a custom item"* and *"AssetBundles"*.
   That wiki page is the maintained reference; this guide just adds our specific bits.

Open the template project in Unity 2022.3.62f1.

---

## Step 2 — Make the item prefab

The simplest reliable way: **duplicate an existing item prefab** from the ripped game assets (the
template explains how to get them), e.g. the **Flashlight**, then swap its model and script.

If building from scratch, create an empty GameObject (right-click in Hierarchy → Create Empty), name
it **`MiniTerminal`**, and on it add (Inspector → *Add Component*):

- **Network Object** (from Netcode). In its Inspector, **UNCHECK "Auto Object Parent Sync"** (this
  is important — it prevents the multiplayer "Only the server can reparent" error).
- **Rigidbody** — set **Use Gravity = off**, **Is Kinematic = on** (match a vanilla item; the
  flashlight's values are a safe copy).
- **Box Collider** — tick **Is Trigger**. Set the GameObject's **Tag** to **`PhysicsProp`** and its
  **Layer** to **`Props`** (top of the Inspector: the Tag and Layer dropdowns).
- Your **model**: drag a mesh in as a child (any small device model, or a cube to start), give it a
  **Mesh Renderer** with a material.
- The **script** from Step 3 (`MiniTerminalItem`).

Drag `MiniTerminal` from the Hierarchy into your **Project** window (a `Prefabs` folder) to make it a
prefab, then delete it from the Hierarchy.

---

## Step 3 — The item script (copy-paste)

Create `Assets/Scripts/MiniTerminalItem.cs` in the Unity project and paste this. It's a normal
`GrabbableObject` (so grab/hold works and Netcode wires it via the bundle build). It does nothing on
its own — the MOD detects this item and does the hack when you use it, so you don't write networking
here.

```csharp
using UnityEngine;

// A held "mini terminal". The AlliedDefenses mod recognises this component and, when the holder
// uses the item (left click), hacks whatever they're aiming at. Keep it minimal.
public class MiniTerminalItem : GrabbableObject
{
    public override void ItemActivate(bool used, bool buttonDown = true)
    {
        base.ItemActivate(used, buttonDown);
        // The mod hooks ItemActivate on this type; nothing else to do here.
    }
}
```

Add this script component to your `MiniTerminal` prefab (Add Component → search `MiniTerminalItem`).

---

## Step 4 — The Item asset + icon

1. In Project → right-click → *Create* → the LC **Item** ScriptableObject (the template adds this
   menu). Name it **`MiniTerminalItemData`**.
2. Fill it in the Inspector:
   - `Item Name`: `Mini Terminal`
   - `Two Handed`: off, `Grabbable`: on, `Can Be Grabbed Before Game Start`: on
   - `Weight`: `1.05` (light)
   - `Item Icon`: a small 128×128 sprite (make any simple PNG, import it, set its *Texture Type* to
     *Sprite (2D and UI)*), drag it here.
   - `Spawn Prefab`: drag your **`MiniTerminal`** prefab here.

---

## Step 5 — Build the AssetBundle

1. Select your prefab, Item asset and icon in the Project window. At the **bottom of the Inspector**
   there's an **AssetBundle** dropdown → create a new bundle named **`alliedminiterminal`** and
   assign these assets to it.
2. Build it: the template has a menu like **Assets → Build AssetBundles** (or a custom build button).
   You'll get a file named `alliedminiterminal` (+ a `.manifest`).

---

## Step 6 — Hand it back to the mod author

Send:
1. the **`alliedminiterminal`** file,
2. the **type name** of your script: `MiniTerminalItem`,
3. the **asset names** you used: prefab `MiniTerminal`, item `MiniTerminalItemData`.

The mod then (small amount of glue code):
- loads the bundle: `AssetBundle.LoadFromFile(...)`,
- registers the network prefab at `GameNetworkManager.Start`,
- registers the shop item (e.g. via **LethalLib** `Items.RegisterShopItem`) so it's buyable, OR reuses
  the existing `ally hack` purchase,
- hooks `MiniTerminalItem.ItemActivate` to run the **same hack** the key tool already does (aim →
  `TerminalAccessibleObject.CallFunctionFromTerminal` via the host).

That reuses everything already built, so the physical item is a thin layer on top.

---

## If you want the real typing screen later

That's a much bigger feature (render a terminal to a texture, capture typed input, network it). Do
the item above first; once it works, we can talk about a screen as a separate, ambitious step.

Good luck — and send screenshots if any step is unclear!

# AlliedDefenses

A **Lethal Company** (BepInEx) mod that lets you **hijack the facility defenses from the ship terminal**. A hijacked defense becomes **allied**: it stops aiming at players and shoots **enemies** instead.

Author: **Remilulz_91** — © 2026 Remilulz_91, all rights reserved (see [License & Copyright](#license--copyright)).

> Status: **working skeleton**. Structure, networking and terminal commands are in place. Most game member names are now confirmed against the vanilla decompiled code; the remaining defensive reflection is noted below.

## Terminal commands

In the ship computer, type:

```
ally <id>        hijack one defense by its id, turret or mine (e.g. ally A0, ally U9)
ally turrets     hijack every turret on the level
ally mines       hijack every mine on the level
ally control <id> take manual remote control of a turret (gun-cam on the monitor)
ally release     give back control of the turret you are driving
ally help        explain how the mod works
ally config      show the current settings (duration, range, friendly fire, cost...)
```

The `ally` keyword is configurable. `ally config` always prints the real, live values, so players can see exactly how the mod is set up — for example: how long a defense stays allied, the enemy detection range, and that allied defenses never hit players.

Both turrets and mines have a terminal id in-game (the same code you'd type to disable them temporarily). So any defense can be hijacked individually by its id, or all defenses of a type can be flipped at once with `ally turrets` / `ally mines`. An allied mine stops exploding under players and instead detonates when an enemy steps close.

While a defense is allied, a live countdown (`m:ss`) is shown right under its code box on the radar map (ship monitor and terminal map view), so you can see at a glance how long each one stays on your side. The plain code is restored automatically when the hijack ends.

### Colour feedback

Allied defenses are colour-coded so you can tell friend from foe, using **two deliberately different colours** to avoid confusion with the game's own green ("active") / red ("disabled") codes:

- **In the dungeon (green):** an allied turret shows a permanent green laser beam from its barrel toward where it's aiming, and its light turns green. The beam is a dedicated LineRenderer we drive ourselves (so it shows continuously, not only in vanilla aiming states); it reuses the game's own laser material so it renders correctly under HDRP.
- **On the ship radar (blue):** the turret/mine code on the map turns blue (not green), so it can't be mistaken for the vanilla "active" green.

Original colours are restored when a defense turns hostile again. Everything lives under `[Visuals]` in the config: `ColorAlliedDefenses` (toggle), `AlliedColorHex` (in-world, default green `00FF00`), `RadarAlliedColorHex` (radar, default blue `1E90FF`).

### Manual remote control

`ally control <id>` lets you take over an allied turret yourself: the ship monitor switches to the turret's gun-cam, you aim with the mouse and fire with LMB (it hits **anything**, players included), and `ally release` (or the release key, default `V`) hands it back. Your aim and shots are streamed to all players so everyone sees the turret move and fire.

This is the most engine-heavy feature, split into clear parts: `TurretControlSession` (state), networker RPCs (begin/end/aim), `ManualControlInput` (local mouse/key), `TurretHijack.DriveManually` (aim + fire), and `TurretMonitorFeed` (the gun-cam on the monitor). The camera-to-monitor rendering and a couple of input details (freezing movement, cursor) are marked `TODO` because they genuinely need tuning in-engine — see the notes in `TurretMonitorFeed.cs`.

---

## Extensible architecture

The mod only knows the abstract idea of a "hijackable defense" (`IHijackableDefense`). Adding a new weapon (mine, etc.) = create a class implementing that interface and register it. No other file needs to change.

```
AlliedDefenses/
├── AlliedDefenses.csproj          # build (NuGet: GameLibs, BepInEx, Netcode)
├── manifest.json                  # Thunderstore metadata (author: Remilulz_91)
├── README.md
└── src/
    ├── Plugin.cs                  # BepInEx entry point, wires everything together
    ├── Config/
    │   └── ModConfig.cs           # settings (keyword, duration, range, cost...)
    ├── Core/
    │   ├── IHijackableDefense.cs   # the common contract for every weapon
    │   ├── DefenseRegistry.cs      # directory: where you plug in a new weapon
    │   ├── HijackManager.cs        # brain: state + expiry + network resolution
    │   ├── HijackTicker.cs         # calls HijackManager.Tick() each frame
    │   ├── TargetingHelper.cs      # nearest enemy + line of sight
    │   ├── TerminalCodeResolver.cs # find a defense from its terminal code
    │   ├── RadarTimerDisplay.cs    # live m:ss countdown on the radar code box
    │   ├── TurretControlSession.cs # who is manually driving which turret
    │   ├── ManualControlInput.cs   # local mouse/key input while controlling
    │   └── TurretVisuals.cs        # green laser/light cue on allied turrets
    ├── Monitor/
    │   └── TurretMonitorFeed.cs    # turret gun-cam on the ship monitor (scaffold)
    ├── Defenses/
    │   ├── TurretHijack.cs         # TURRET module (allied aiming/firing)
    │   └── MineHijack.cs           # MINE module (player-safe, detonates on enemies)
    ├── Networking/
    │   ├── HijackNetworker.cs      # NetworkBehaviour: server/client RPCs
    │   ├── NetworkObjectManager.cs # registers + spawns the network object
    │   └── NetcodePatcher.cs       # activates the generated RPCs (see NETWORKING)
    ├── Terminal/
    │   └── CommandText.cs          # English text for help / config / usage
    └── Patches/
        ├── TurretPatches.cs        # bypasses an allied turret's Update
        ├── MinePatches.cs          # makes an allied mine ignore players
        └── TerminalPatches.cs      # the "ally" commands in the computer
```

Hijack flow: **Terminal** → `HijackManager.RequestHijack` → `HijackNetworker` (client→server→everyone) → `HijackManager.ApplyHijack` on each machine → the module sets the allied state → patches change targeting.

---

## Build

Requires the [.NET SDK](https://dotnet.microsoft.com/download) (6 or 8).

```bash
cd AlliedDefenses
dotnet build -c Release
```

Output: `bin/Release/AlliedDefenses.dll`.

The `.csproj` pulls the game DLLs (`LethalCompany.GameLibs.Steam`), BepInEx and Netcode from NuGet automatically. **No manual copy of `Assembly-CSharp.dll` is needed.** Just adjust the GameLibs package version to match your game build if necessary.

---

## NETWORKING — required step: the Netcode Patcher

The mod uses custom RPCs (`[ServerRpc]` / `[ClientRpc]`). In a Unity *Netcode for GameObjects* game, these methods must be "woven" into the `.dll` by a tool **after** compilation: the **Netcode Patcher** (by Evaisa).

1. Get the tool: https://github.com/EvaisaDev/UnityNetcodePatcher
2. Run it on the compiled `.dll`, e.g.:

```bash
netcode-patch ./bin/Release/AlliedDefenses.dll <paths to the Netcode/Unity DLLs>
```

Without this step, hijacking won't sync between players (the RPCs do nothing). `NetcodePatcher.cs` activates at runtime what the tool generated, but does not replace it.

---

## Install / test

1. Install **BepInEx** on the game (easiest: the **r2modman** mod manager).
2. Copy the netcode-patched `AlliedDefenses.dll` into `Lethal Company/BepInEx/plugins/`.
3. Launch the game: the BepInEx console should show `AlliedDefenses v0.1.0 by Remilulz_91 loaded`.
4. In a round, open the terminal and type `ally <id>` with a turret's id (or `ally help`).

In multiplayer, **every player must have the mod** (same version).

---

## Confirmed game API (verified against vanilla code)

These names are confirmed from the decompiled vanilla classes (cross-checked with the `lc-turret-key` and `MissileTurret` mod sources):

Turret: `turretActive`, `turretMode` (enum `TurretMode`, e.g. `Detection`), `turretModeLastFrame`, `rotationSpeed`, `rotatingClockwise`, `turretAnimator` (`SetInteger("TurretMode", n)`), `bulletParticles`, `centerPoint` (pivot transform), `ToggleTurretEnabledLocalClient(bool)`, `Start()`, `Update()`.

EnemyAI: `HitEnemy(int force, PlayerControllerB playerWhoHit, bool playHitSFX, int hitID)`, `isEnemyDead`; living monsters listed in `RoundManager.Instance.SpawnedEnemies`.

Landmine: `hasExploded`, `PressMineServerRpc()`, `TriggerMineOnLocalClientByExiting()` (private, drives the networked explosion), `OnTriggerEnter(Collider)` / `OnTriggerExit(Collider)`.

Monitor / manual control: `StartOfRound.Instance.mapScreen` (a `ManualCameraRenderer`) whose radar camera renders the ship monitor; `PlayerControllerB.DamagePlayer(...)` for hitting players. These (plus the camera-to-RenderTexture feed) are the bits to verify/tune in-engine — see `TurretMonitorFeed.cs`. OpenBodyCams is the reference for a polished monitor feed.

Still using defensive reflection (in case a future game update renames it): `TerminalAccessibleObject`'s code string (tries `objectCode`, then fallbacks), its on-map text `mapRadarText` (a TextMeshPro, used for the radar countdown), and `Terminal.screenText` / `Terminal.textAdded`. Confirm these in your decompiler (dnSpy / ILSpy) and you can switch them to direct access.

---

## Adding a new weapon

The turret and mine modules show the two supported patterns:

- **Active aiming (turret)**: drives its own aiming/firing each frame (here via a patch that bypasses the vanilla Update).
- **Passive trigger (mine)**: no aiming loop; reacts via `TickAlliedTargeting` (detonates on a nearby enemy) plus a patch that keeps players safe.

Both are resolvable by terminal id through the shared `TerminalCodeResolver`, and both also support the generic group command `ally <type>s`.

To add another defense, create a class implementing `IHijackableDefense`, then register it in `DefenseRegistry.RegisterDefaults()`. The terminal (including the generic `ally <type>s` group command), networking and expiry all work automatically — no other file needs to change.

---

## Going further (ideas)

- **More faithful aiming**: instead of bypassing the turret's `Update`, patch only its player-detection method to target an enemy → keeps native animations and sounds.
- **Credit cost** (`HijackCreditCost`) and a global cooldown, validated server-side.
- **First-person turret view** as an alternative to the monitor gun-cam.

---

## License & Copyright

© 2026 **Remilulz_91**. All rights reserved.

This mod and its source code are the property of their author, Remilulz_91. You are welcome to:

- **download, install and play** the mod, and
- **contribute** to its development (report issues, submit pull requests, or fork it for the purpose of contributing back).

You may **not**:

- claim authorship or ownership of this mod or any part of its code,
- redistribute it as your own work, or publish modified copies under a different author, without the author's permission.

The mod remains credited to and owned by Remilulz_91. See the `LICENSE` file for the full notice.

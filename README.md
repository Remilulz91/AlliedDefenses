# AlliedDefenses

Hijack the facility's defenses from the ship terminal and turn them against the monsters.
A hijacked turret or mine becomes **allied**: it stops targeting players and goes after the
**enemies** instead — and you can even take **manual remote control** of a turret from the ship.

Author: **Remilulz_91** — © 2026 Remilulz_91, all rights reserved.

> ⚠️ **Multiplayer: EVERY player must install this mod (same version).** It adds
> synced behaviour, so it won't work if only the host has it. Playing solo is fine.

---

## Requirements

- **BepInExPack**
- **OpenBodyCams** — used to show the turret's view on the ship monitor when you take
  manual control. (Installed automatically as a dependency.)

---

## Terminal commands

At the ship computer, type:

```
ally <id>          hijack one defense by its id (turret or mine), e.g. ally U9
ally turrets       list all turrets on the level and their ids
ally mines         list all mines on the level and their ids
ally control <id>  take manual remote control of a turret
ally release       give back the turret you are controlling
ally help          explain how the mod works
ally config        show the current settings
```

The `<id>` is the same code you'd use to disable a turret/mine (the one shown on the radar
map). Use `ally turrets` / `ally mines` to find the ids. The keyword `ally` is configurable.

---

## What it does

**Allied turrets.** A hijacked turret stops shooting players and instead aims at and fires on
the nearest visible enemy in range. It shows a green laser beam toward its target, and its
light turns green so you can spot it in the dungeon.

**Allied mines.** A hijacked mine no longer explodes under players — it only detonates when an
**enemy** steps close. Its light turns green while it's on your side.

**On the radar.** An allied turret/mine's code on the ship map turns **blue** (so it's not
confused with the game's green "active" / red "disabled"), with a live **countdown** showing
how long it stays allied.

**Manual remote control.** `ally control <id>` lets you drive a turret yourself from the ship:
the **ship monitor shows the turret's view**, you **aim with the mouse**, and **left-click
fires** (it can hit anything, players included). Press the release key (default **V**) or type
`ally release` to hand it back. The view is provided by OpenBodyCams.

Hijacks last a set time (60s by default), then the defense turns hostile again. Everything
(duration, detection range, colours, friendly-fire, manual-control settings) is adjustable in
the config — `ally config` shows the current values in-game.

---

## Install

Easiest with **r2modman** (or any mod manager): select Lethal Company, install AlliedDefenses,
and its dependencies (BepInEx, OpenBodyCams) come along. Launch with **Start modded**.

Make sure **every player in the lobby** has the mod, same version.

---

## License & Copyright

© 2026 **Remilulz_91**. All rights reserved.

You may download, play, and contribute to this mod (issues, pull requests). You may **not**
claim authorship/ownership of it or its code, or redistribute it as your own work, without the
author's permission. The mod remains credited to and owned by Remilulz_91.

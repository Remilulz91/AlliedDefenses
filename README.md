# AlliedDefenses

Hijack the facility's defenses from the ship terminal and turn them against the monsters.
A hijacked turret, mine or spike trap becomes **allied**: it stops targeting players and goes
after the **enemies** instead. Then spend your team's credits on **upgrades**, and deploy a
carryable **Defense Beacon** that protects you out in the field — even against the enemies you
can't kill.

Author: **Remilulz_91** — © 2026 Remilulz_91, all rights reserved.

## Features

- 🔧 **Hijack the facility's defenses** — turrets, mines and spike traps become **allied** and
  turn on the monsters instead of you.
- 💰 **Team-wide upgrades** — bought once from shared credits, everyone benefits: turret damage,
  fire rate, range, hijack duration, mine radius, and the counter-play auras below.
- 📡 **Deployable Defense Beacon** — buy it once, then drop it at your position with a key,
  anywhere in the field (where there are no turrets/mines), to project your protection. Radius ring
  shown in-world **and on the ship monitor**.
- 🖥️ **Hack Tool** — buy it, then aim at a locked big door, turret, mine or spike trap and press a
  key to trigger it — like typing its code at the terminal, but from inside the facility.
- 🛡️ **Counter-play vs 7 UNKILLABLE enemies** — hide from / disable the things you can't shoot:
  Ghost Girl, Coil-Head, Earth Leviathan (sand worm), Eyeless Dogs, Barber, Hygrodere, Circuit Bees.
- 🤝 **Co-op synced** — upgrade levels and the beacon are shared across the lobby, including
  players who join mid-game.
- 🌍 **English & French** terminal help, plus a fully configurable `.cfg`.

> ⚠️ **Multiplayer: EVERY player must install this mod (same version).** It adds synced
> behaviour, so it won't work if only the host has it. Playing solo/host is fine.

---

## Terminal commands

Type these at the ship computer. The keyword `ally` is configurable (see the config).

| Command | What it does |
|---|---|
| `ally` | Show the short command list (same as below). |
| `ally <id>` | **Hijack one defense** by its id, e.g. `ally U9`. The id is the same code you'd use to disable that turret/mine/spike (the one on the radar map). |
| `ally turrets` | List every **turret** on the level and its id. |
| `ally mines` | List every **mine** on the level and its id. |
| `ally spikes` | List every **spike trap** on the level and its id. |
| `ally upgrades` | Show all **upgrades**, their level, effect and next cost, plus your credits and save mode (see below). |
| `ally upgrade <id>` | **Buy the next level** of an upgrade with ship credits, e.g. `ally upgrade turretdamage`. |
| `ally upgrade reset` | Reset **all** upgrades back to level 0. |
| `ally beacon` | **Buy the Defense Beacon** (one-time); deploy it with the deploy key. |
| `ally hack` | **Buy the Hack Tool** (one-time); use it by aiming + the hack key. |
| `ally help` | Explain how the mod works (also `ally info`). |
| `ally config` | Show the current settings, read live from the config. |

---

## What `ally upgrades` shows

The command prints your credits, the save mode, then every upgrade with its current level,
its effect at that level, and the cost of the next level. Example:

```
ALLIED DEFENSES - UPGRADES
------------------------------------
Credits: 500
Save mode: kept through game over

duration     Lv 0/10  (60s allied)             next: 120 cr
turretdamage Lv 0/25  (1 dmg/shot)             next: 100 cr
firerate     Lv 0/6   (4.8 shots/s)            next: 130 cr
turretrange  Lv 0/8   (30m detect)             next: 100 cr
mineradius   Lv 0/6   (4m radius)              next: 90 cr
sanity       Lv 0/5   (off (Ghost Girl))       next: 150 cr
neutralize   Lv 0/5   (off (Coil-Head))        next: 200 cr
seismic      Lv 0/5   (off (Earth Leviathan))  next: 180 cr
muffle       Lv 0/5   (off (Eyeless Dog))      next: 160 cr
barber       Lv 0/5   (off (Barber))           next: 170 cr
slime        Lv 0/5   (off (Hygrodere))        next: 140 cr
bees         Lv 0/5   (off (Circuit Bees))     next: 120 cr
beacon       Lv 0/1   (not owned)              next: 175 cr
slots        Lv 0/3   (1 beacon(s) at once)    next: 200 cr

Buy with 'ally upgrade <id>'.
```

Upgrades are **team-wide**: bought once from the shared ship credits, every player in the lobby
benefits (levels are synced to everyone, and players who join mid-game get the current levels).
The cost of each level **scales up** the more levels you already own.

---

## Upgrades reference

| id | Effect per level | Max | Base cost |
|---|---|---|---|
| `duration` | +20s to how long a hijack stays allied | 10 | 120 |
| `turretdamage` | +1 turret damage per shot to enemies (high cap, to down tough enemies like the Forest Keeper) | 25 | 100 |
| `firerate` | Faster allied-turret fire rate (≈4.8 → ≈12.5 shots/s) | 6 | 130 |
| `turretrange` | +5m turret enemy-detection range | 8 | 100 |
| `mineradius` | +1m allied-mine trigger radius | 6 | 90 |
| `sanity` | Ghost Girl counter-play: bleeds your hidden insanity while near an allied defense/beacon, so she targets/escalates on you less (radius + rate grow per level) | 5 | 150 |
| `neutralize` | Coil-Head counter-play: an allied turret watching a Coil-Head freezes it; higher levels keep it frozen longer after the turret loses sight | 5 | 200 |
| `seismic` | Earth Leviathan (sand worm) counter-play: while near an allied defense/beacon the worm can't target you (radius grows per level) | 5 | 180 |
| `muffle` | Eyeless Dog counter-play: noises made inside the radius are silenced, so the (blind) dogs don't hear you | 5 | 160 |
| `barber` | Barber counter-play: while near an allied defense/beacon the Barber can't target you, so it won't dance/lunge toward you (radius grows per level) | 5 | 170 |
| `slime` | Hygrodere (slime) counter-play: while near an allied defense/beacon the blob can't target you, so it wanders off instead of following (radius grows per level) | 5 | 140 |
| `bees` | Circuit Bees counter-play: they still notice you near their hive, but while you're in the radius they drop the chase and return to the hive (weaker: disengage, not full stealth) | 5 | 120 |
| `beacon` | Unlocks the Defense Beacon (this is what `ally beacon` buys) | 1 | 175 |
| `slots` | Deploy more than one beacon at once (1 + level, so up to 4) | 3 | 200 |

Level 0 means **off** for the counter-play auras (`sanity`, `neutralize`, `seismic`, `muffle`,
`barber`, `slime`, `bees`) — buy at least level 1 to enable them.

---

## The Defense Beacon

Outside, around the ship, there are no turrets or mines — so the counter-play auras had nothing
to anchor to. The **Defense Beacon** fixes that.

- Buy it once with `ally beacon` (costs `BeaconPrice`, default 175 credits). Bought once, never
  re-paid.
- **Deploy it** by pressing the deploy key (`BeaconDeployKey`, default **B**) — it drops at your
  position, anywhere in the field. Press again to **move it** to your new position. `ally upgrade
  reset` removes it.
- Wherever it sits, it acts as an **allied-defense anchor**, so the `sanity` / `seismic` / `muffle`
  / `barber` / `slime` / `bees` auras work around it — out by the sand worm, near the dogs, wherever.
- A **magenta ring** on the ground shows the current aura radius. It also appears on the **ship
  monitor** (radar map) when the camera is looking near the beacon.

The beacon is **inert on purpose** — it hides/deters, it does not shoot. It is a simple deployed
object (not a carried item), which keeps it rock-solid in multiplayer.

---

## Hack Tool

Buy it once with `ally hack` (costs `HackToolPrice`, default 150). Then, from **inside the facility**,
**aim** at a locked **big door**, **turret**, **mine** or **spike trap** within range (`HackRange`,
default 6 m) and press the **hack key** (`HackKey`, default **H**) to trigger it — exactly like typing
its code at the ship terminal (open the door / temporarily disable the turret), but on the spot. It
runs the game's own terminal function via the host, so it's reliable in multiplayer.

---

## Allied defenses (turrets, mines, spikes)

- **Allied turret** — stops shooting players and fires on the nearest visible enemy in range.
  Shows a green laser toward its target; its light turns green so you can spot it.
- **Allied mine** — no longer explodes under players; it detonates only when an **enemy** steps
  close. Green light while it's on your side.
- **Allied spike trap** — no longer crushes players; still slams down on **enemies** underneath.

**On the radar/monitor**, an allied defense's code turns **blue** (not the game's green "active" /
red "disabled"), with a live **countdown** of how long it stays allied. Hijacks last a set time
(60s by default), then the defense turns hostile again.

---

## Configuration

Settings live in `BepInEx/config/Remilulz_91.AlliedDefenses.cfg` (created on first launch).
`ally config` shows the live values in-game.

**[General]**
- `HijackCommand` = `ally` — the terminal keyword.
- `Language` = `English` — terminal help language (`English` or `Francais`).
- `HijackDuration` = `60` — seconds a defense stays allied (`0` = unlimited).

**[Targeting]**
- `EnemyDetectionRange` = `30` — turret enemy-detection distance (m).
- `MineTriggerRadius` = `4` — allied-mine detonation radius (m).
- `IgnorePlayersWhenAllied` = `true` — allied defenses never hurt players (recommended).

**[Economy]**
- `HijackCreditCost` = `0` — credit cost to hijack a defense (0 = free).
- `TurretEnemyDamage` = `1` — base turret damage per shot (before `turretdamage`).
- `EnableUpgrades` = `true` — enable the buy-with-credits upgrade system.
- `UpgradePersistence` = `Persistent` — `Persistent` keeps upgrades forever (survive death AND
  game over); `PerSave` ties them to the save slot (wiped on a game over).

**[Beacon]**
- `EnableBeacon` = `true` — enable the carryable Defense Beacon.
- `BeaconPrice` = `175` — one-time cost.
- `BeaconRingOnMonitor` = `true` — also draw the radius ring on the ship monitor.
- `BeaconRingColorHex` = `FF2DD0` — ring colour (magenta), a colour the map doesn't use.
- `BeaconDeployKey` = `B` — key to deploy/move the beacon at your position (Input System key name).
- `BeaconRecallKey` = `N` — key to recall/store the deployed beacon (or `ally beacon recall`).
- `BeaconRadarTarget` = `true` — show the beacon as a selectable target on the ship radar/monitor.

**[HackTool]**
- `EnableHackTool` = `true` — enable the Hack Tool.
- `HackToolPrice` = `150` — one-time cost (`ally hack`).
- `HackKey` = `H` — key to hack the object you're aiming at (Input System key name).
- `HackRange` = `6` — max hack distance (m).

**[Visuals]**
- `ColorAlliedDefenses` = `true` — tint allied defenses (laser/light + radar code).
- `AlliedColorHex` = `00FF00` — in-world allied colour (green).
- `RadarAlliedColorHex` = `1E90FF` — radar-map allied colour (blue).

---

## Install

Easiest with **r2modman** (or any mod manager): select Lethal Company, install AlliedDefenses,
and BepInEx comes along. Launch with **Start modded**.

Make sure **every player in the lobby** has the mod, same version.

---

## License & Copyright

© 2026 **Remilulz_91**. All rights reserved.

You may download and play this mod, and **report bugs or problems via GitHub issues** (tell me
where the problem is and I fix it myself). Code contributions are **not** accepted — pull requests
are disabled on the repository. You may **not** claim authorship/ownership of it or its code, submit
or merge code changes, or redistribute it as your own work, without the author's permission. The
mod remains credited to and owned by Remilulz_91.

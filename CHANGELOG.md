# Changelog

## 0.1.9
- `ally control` with no id now takes over the NEAREST turret (handy for solo/testing
  where you can't read a turret's terminal code).

## 0.1.8
- Turrets now actually aim: rotate the real `turretRod` (RotatingRodContainer) toward
  the target instead of the non-rotating centerPoint. Beam and shots come from the
  muzzle (`aimPoint`). Fixes the "stares at the wall / frozen" behaviour.
- Manual control and the monitor gun-cam follow the rotating rod too.

## 0.1.7
- Better turret diagnostic: logs the full prefab tree and the Turret's Transform
  fields (reflection) once, to identify the real rotation node.

## 0.1.6
- Fix: NullReferenceException spam after a mine exploded. Destroyed defenses are now
  detected (Unity-null) and dropped from the active list instead of being ticked.
- Per-defense tick is wrapped in try/catch so one bad object can't flood the logs.

## 0.1.5
- Allied mines now glow green (in-world) so you can tell they're hijacked.
- Allied turrets idle-sweep instead of freezing when no enemy is in range.
- One-time turret hierarchy log (when a turret is hijacked) to pinpoint the real
  rotation pivot — a tuning aid for the aiming.

## 0.1.3
- Ships the network handler fix (missing class-level [HarmonyPatch] on
  NetworkObjectManager) in a fresh version.

## 0.1.2
- Fix: the network handler is now actually spawned. NetworkObjectManager was missing
  its class-level [HarmonyPatch], so Harmony's PatchAll ignored it and the handler
  never registered/spawned (commands replied "Network handler not ready yet").
- Robust spawn on StartOfRound.Start (host-only, spawn-once) + diagnostic logging.

## 0.1.1
- More robust network handler spawn (on StartOfRound.Start, host-only, spawn-once).
- Added diagnostic logging around prefab registration and spawn.

## 0.1.0
- Initial version.
- Hijack turrets and mines from the ship terminal: `ally <id>` (e.g. `ally U9`),
  or whole groups with `ally turrets` / `ally mines`.
- Allied defenses target enemies instead of players (no friendly fire).
- `ally control <id>`: manual remote control of a turret from the ship monitor
  (mouse aim, LMB fire), `ally release` to stop.
- `ally help` and `ally config` terminal commands.
- Live `m:ss` countdown on the radar code box.
- Colour feedback: green laser/light in the dungeon, blue code on the radar.
- Multiplayer-synced.

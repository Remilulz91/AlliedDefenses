# Changelog

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

# Changelog

## 0.4.0
- Removed the manual remote turret control system entirely: the `ally control` / `ally release`
  commands, the mouse-aim / left-click-to-fire, and the OpenBodyCams dependency are gone.
- The mod now focuses on the allied turrets & mines features (hijack by id, auto-target enemies,
  colour cues, radar timer).

## 0.3.5
- Fix terminal commands failing for a player after they disconnect and reconnect: the
  network handler reference is now re-acquired if the old one was destroyed (and cleared
  on despawn) instead of staying stale.

## 0.3.4
- Updated required dependencies to their latest versions: BepInExPack 5.4.2305,
  OpenBodyCams 3.0.12.

## 0.3.3
- Fixed the GitHub website link (Remilulz91, no underscore).

## 0.3.2
- Workflow: the Thunderstore publish step is skipped cleanly when no TS_TOKEN is set
  (so the build stays green); set the secret to enable auto-publish.
- Added website_url to the manifest.

## 0.3.1
- OpenBodyCams added as a required dependency (for the manual-control monitor view).
- README rewritten for players (what the mod does + how to use it); dev/build details moved
  to the separate guide files.

## 0.3.0
- Automatic Thunderstore publishing from GitHub on each version tag (see PUBLISH_THUNDERSTORE.md).

## 0.2.9
- Reduce "see through walls" in the turret view: the camera now sits on a mount placed a
  bit behind the muzzle (in open space) instead of inside the wall the turret is bolted to.

## 0.2.8
- Fix tilted/rolling turret view: aiming now keeps "up" toward world up (no roll drift),
  so the body-cam image stays level.
- Brief one-shot muzzle flash per shot (left-click to fire) for clear feedback.
- "ally control <id>" now auto-releases the turret you were controlling, so you can
  switch turrets without pressing the release key first.

## 0.2.7
- Removed the control light entirely (it caused the white-out and stayed enabled via the
  saved config even after the default was flipped). The turret view now always uses the
  body cam's natural auto-exposed image.

## 0.2.6
- Control now respects the 60s hijack timer (it expires and control ends with it).
- Removed "ally control" (nearest); use "ally control <id>" (find ids with "ally turrets").
- Control light OFF by default (HDRP auto-exposure was blowing it out to white); the body
  cam's natural auto-exposed view is used instead. Still tunable in the config.

## 0.2.5
- "ally turrets" / "ally mines" now LIST the defenses and their ids (instead of hijacking
  all of them), so you can pick one to hijack/control.
- Fix control ending instantly on an already-allied turret: taking control now refreshes
  the timer, and a turret won't expire while it's being controlled.
- Fix white-out monitor: control light intensity lowered (default 200) and pushed ahead
  of the muzzle so it lights the scene, not the camera.

## 0.2.4
- Robust networking: the host now applies hijack/control/aim DIRECTLY (locally) and only
  uses RPCs to mirror to remote clients, wrapped so a failure is non-fatal. Fixes the
  "RPC hash not found" crashes that blocked control in solo, regardless of netcode-patch
  reliability.

## 0.2.3
- Fix endless firing: removed the looping muzzle particle Play() that never stopped.
- Add a control light on the turret while driving it, so its dark facility view is
  visible on the monitor (configurable intensity, HDRP-aware).

## 0.2.2
- Fix RPC "hash not found" spam: aim is now applied locally and broadcast at ~20 Hz via
  the working host->ClientRpc path (host no longer calls a ServerRpc on itself).
- Fix mouse sensitivity (was multiplied by deltaTime, making it crawl).
- Fix fire that wouldn't stop (firing state now updates locally every frame).

## 0.2.1
- Remote turret control via OpenBodyCams (soft dependency): the ship monitor shows the
  turret's view, you aim with the mouse and fire with LMB. Restored to the player on
  release. Without OpenBodyCams the turret still obeys the mouse (no remote view).

## 0.2.0
- Manual control reworked: the turret now follows where you LOOK (no more buggy
  monitor gun-cam). LMB fires. The ship monitor keeps its normal radar view.

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

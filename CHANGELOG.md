# Changelog

## 0.11.1
- New counter-play: the HYGRODERE / slime (BlobAI). It picks the closest targetable player via
  TargetClosestPlayer -> PlayerIsTargetable; when no one is targetable it just roams (verified in
  the DLL - graceful, no null crash). So near an allied defense/beacon the slow blob wanders off
  instead of following. New 'slime' upgrade (level 0 = off). Added to the shared untargetable patch.

## 0.11.0
- New counter-play: the BARBER (ClaySurgeonAI). It dances toward the closest targetable player via
  TargetClosestPlayer -> PlayerIsTargetable, so — like the sand worm — a player standing inside an
  allied-defense/beacon radius is made untargetable and the Barber won't jump at them. New 'barber'
  upgrade (level 0 = off, radius grows per level). The untargetable patch now covers both the worm
  (seismic) and the Barber (barber).
- Beacon ring colour: both rings are now magenta/pink (config Beacon/BeaconRingColorHex, default
  FF2DD0) so they can't be confused with the game's map colours (green terrain, blue exit line).
  The monitor ring recolours an INSTANCE of the radar material, so the game's own exit line is
  untouched. The in-world beacon light stays green (the "allied" glow).

## 0.10.1
- Beacon radius on the ship monitor: a second ring is drawn on the radar-map layer (copying the
  layer + material from the game's own map exit-line), so the beacon's radius shows on the ship
  monitor too. It only appears when the monitor is looking near the beacon (the camera follows
  players), as expected. Toggle with config 'Beacon/BeaconRingOnMonitor' (default on).

## 0.10.0
- Multiplayer sync (step 3): upgrades are now TEAM-WIDE. When any player buys an upgrade (or the
  beacon) from the shared ship credits, the new level is broadcast to the whole lobby, so every
  player benefits and the host (which drives enemy behaviour for seismic/muffle) always has the
  right levels. Late joiners request the full level set from the host on connect. The host persists
  team upgrades so they survive restarts, even ones a client bought. If a client buys the beacon,
  the host delivers the physical beacon on its side. All best-effort over the existing networker
  (solo/host play is unaffected if a build isn't fully netcode-patched).

## 0.9.9
- Cleanup: dropped the non-working "upright" attempt (the donor item rests on its side by design)
  and its BeaconUpright config. Removed the testing-only 'ally givecredits' command and its
  EnableDevCommands config. Trimmed verbose logs (kept load, defense registration, network spawn,
  beacon delivery, and all errors/warnings).

## 0.9.8
- Upright fix take 2: forcing the rotation in Update didn't stick because GrabbableObject.LateUpdate
  (which sets the resting rotation) runs afterwards and overwrote it. Moved the upright logic into a
  LateUpdate override (after base.LateUpdate), so it now holds. Still behind BeaconUpright.

## 0.9.7
- Placed beacon now stands upright (the donor model otherwise lay on its side when dropped). Only
  pitch/roll are flattened, yaw is kept. New config 'Beacon/BeaconUpright' (default true) — set it
  to false to roll back to the vanilla resting behaviour.

## 0.9.6
- Radius ring was drawn in the beacon's local space, so it tipped vertical (an arch into the sky)
  when the beacon was rotated. It is now drawn in world space as a flat horizontal circle on the
  ground under the beacon, whatever the beacon's orientation.

## 0.9.5
- Beacon visuals finished: model (Fancy lamp), inventory icon, held position and ground radius
  ring all working. Removed the verbose [BeaconVisuals] diagnostic log (kept a one-line note of
  which donor model is used).

## 0.9.4
- Held view: the old positionOffset (0,-0.3,0.6) shoved the beacon off the bottom-right of the
  screen. LateUpdate sets the held item's localPosition to positionOffset relative to the item
  holder, so it is now (0, 0.1, 0.25) to sit naturally in view. Placed look (0.7 m) unchanged.

## 0.9.3
- Better beacon model: the Apparatus donor was long/thin on one axis (size Z=3.69), so it
  normalised to a flat object that looked offset from the light and was invisible when held. Now
  we prefer compact, upright donors (Fancy lamp first) and skip any mesh with a >3:1 aspect ratio.
  The glow light is moved to the model centre instead of a fixed height above it.

## 0.9.2
- Model alignment: switch to deterministic pivot centring (renderer.bounds is cached and lied
  right after the mesh swap) and log the mesh centre/size/scale to diagnose the remaining offset.

## 0.9.1
- Fix the borrowed model being offset from the collider/grab point (and invisible when held):
  the donor mesh is now aligned to the beacon pivot using the renderer's real world bounds instead
  of mesh-local maths, so the model sits exactly on the grab point and shows in hand.

## 0.9.0
- Beacon now looks like a real item: at runtime it borrows a vanilla item's 3D model AND its
  inventory icon (no asset bundle), so the in-hand view and the inventory slot are no longer empty.
- Aura radius indicator: a green ring is drawn on the ground around a placed beacon, sized to the
  largest active aura radius (sanity/seismic/muffle) and updating with your upgrades. Hidden while
  carried. Visible in-world and through the ship's monitor camera.
- Removed the temporary [GrabProbe] grab-detection diagnostic (grabbing works). [BeaconDiag] on
  Start also removed.

## 0.8.10
- Grabbing works now. Fix the "invisible while carried" look: the centre pivot put the camera
  inside the mesh when held. The held beacon is now offset forward/down (positionOffset) so it
  sits in front of the camera, and its light is switched off while carried (glows only when placed).

## 0.8.9
- THE grab fix: the beacon was on layer 8, which is the ship's own geometry layer ("ShipInside"
  is layer 8). The grab code treats layer 8 (and 30) as "not a grabbable" before it ever checks
  the PhysicsProp tag, so the beacon was classified as a wall. Moved it to layer 6 ("Props", the
  real grabbable-item layer, still in the interact mask), so the PhysicsProp tag now routes it to
  the grab path. Bonus: layer 6 no longer blocks the player's movement.

## 0.8.8
- Temporary [GrabProbe] diagnostic (dev commands only): replays the game's exact grab ray + LOS
  check while you aim at the beacon and logs what it sees, to find why the prompt never shows.

## 0.8.7
- Beacon grab, take 2: the item's pivot was at its base (floor level), and the game's grab
  line-of-sight check traces to the pivot — a floor-level pivot is blocked by the floor itself.
  The beacon's visuals/collider are now centred on the pivot and it rests with the pivot 0.5 m
  off the floor (verticalOffset 0.5), so the line-of-sight is clear. Diag now also logs the
  collider enabled/trigger state.

## 0.8.6
- Fix beacon grab: it spawned ~2.8 m up near a shelf (floating/embedded), so the game's
  line-of-sight check to the item failed and no grab prompt showed. It now spawns on open ship
  floor (a player spawn point), raycasts down to rest exactly on the floor, and is marked settled
  so it skips the falling animation. Layer/tag were already correct (confirmed by [BeaconDiag]).

## 0.8.5
- Temporary diagnostic: the beacon logs its runtime layer/tag/position on Start ([BeaconDiag])
  to pin down why grabbing fails. To be removed once grab works.

## 0.8.4
- Fix: the Defense Beacon could not be picked up. PlayerControllerB.BeginGrabObject requires the
  hit collider's GameObject to be on layer 8 AND tagged "PhysicsProp"; the beacon was on the wrong
  layer and had no tag. It now sets layer 8 + "PhysicsProp" and uses a single non-trigger collider.
- Toned down the beacon light/emission (it was washing the whole room bright green).

## 0.8.3
- Testing aid (opt-in, off by default): config 'Dev/EnableDevCommands'. When true, the terminal
  accepts 'ally givecredits <n>' to add ship credits so beacon/upgrade purchases can be tested
  without grinding. Meant to be removed/left disabled for real play.

## 0.8.2
- Fix a looping NullReferenceException in the menu (GrabbableObject.FallWithCurve): the beacon
  network-prefab template was left active, so its Update ran every frame with no StartOfRound.
  The template is now kept inactive and only the spawned copy is activated (before its Spawn).

## 0.8.1
- Build fix: removed an invalid `Item.grabbable` assignment in BeaconFactory (grabbable
  belongs to GrabbableObject, which is already set). No behaviour change from 0.8.0.

## 0.8.0
- DEFENSE BEACON: a carryable, two-handed heavy prop bought once with `ally beacon`
  (price configurable, default 175 cr). Delivered to the ship, carried out into the
  field (no loot while carrying, slower move), and set down anywhere to anchor the
  counter-play auras where there are no turrets or mines — near the ship, by the worm,
  wherever. Bought once and re-delivered free if lost. Built entirely at runtime (no
  asset bundle) and spawned via the mod's own networking — no new dependency.
- Two new counter-play auras, now usable outdoors thanks to the beacon:
  - `seismic`: the Earth Leviathan (SandWormAI) hunts by proximity, not sound, and is
    unkillable. While you stand in the radius, EnemyAI.PlayerIsTargetable is forced false
    for the worm only, so it can't pick you (and drops you if already chasing).
  - `muffle`: Eyeless Dogs (MouthDogAI) are blind and hunt by sound. Noises made inside
    the radius are dropped in MouthDogAI.DetectNoise, so the dogs never hear you there.
- `haul` upgrade: lowers the beacon's carry weight (~47 lb down to ~16 lb) so you move
  faster while carrying it (floored, never weightless).
- The `sanity`/`seismic`/`muffle` auras now trigger near a placed beacon as well as near
  a hijacked in-facility defense (AnyAlliedWithin also checks the beacon registry).

## 0.7.0
- Counter-play against the "unkillable" enemies, via two new upgrades (both off by default):
  - `sanity`: while you stand near an allied defense, your hidden insanity bleeds down, so
    the Ghost Girl targets and escalates on you less (suppress/delay, not an off switch).
  - `neutralize`: an allied turret watching a Coil-Head freezes it in place (host-side;
    the freeze lingers a moment after the turret loses sight, scaling with the level).

## 0.6.2
- Two more upgrades: turret range (+5m/level) and allied-mine radius (+1m/level).
  `ally config` now shows the effective (upgraded) duration, range and mine radius.

## 0.6.1
- Configurable save mode (`UpgradePersistence`):
  - Persistent (default): upgrades kept forever on this install, even through a game over.
  - PerSave: upgrades tied to the current save slot (per slot), wiped on a game over —
    stored in the game save via ES3, with a guarded game-over hook.

## 0.6.0
- Upgrade system (foundation): buy upgrades with ship credits via the terminal
  (`ally upgrades`, `ally upgrade <id>`, `ally upgrade reset`). First two upgrades:
  hijack duration and turret damage.

## 0.5.0
- New allied defense: SPIKE TRAPS. `ally <id>` / `ally spikes`. An allied spike trap no
  longer crushes players but still slams down on enemies caught underneath.

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

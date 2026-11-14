# Project status

_Last updated: 2026-11-14_

## Current phase: 2 — Core gameplay (Phase 3 monster started early, see below)

### Phase 1 — Playable foundation: verified

Verified by Chad on Windows (2026-10-21) and committed to `main`, including the generated scene,
materials and settings.

- Unity 6 URP project skeleton: package manifest, `.gitignore`, `.gitattributes` with Git LFS.
- First-open bootstrap that configures URP (Forward+, HDR, additional-light shadows, linear colour)
  and generates the Phase 1 scene.
- First-person player prefab: CharacterController movement (walk 3.5 m/s, sprint 6 m/s, jump,
  gravity), mouse look with pitch clamp, cursor lock/unlock.
- Input layer that works with either the Input System package or the legacy Input Manager.
- Hand-held flashlight (F) with soft shadows, slight trailing sway and a click sound.
- Graybox industrial facility (51 m x 45 m) built from a text grid: storage shelves, tank room,
  crates, pillars, a ramp up to a mezzanine, steady and flickering ceiling lamps.
- Night-time block-out of the Route 94 area of Wadsworth, OH around the facility: Route 94,
  Water, Chestnut, Pine, Park, Garfield, Walnut and Bergey streets, houses east of 94 and along
  State/Walnut, the pond north-west of the facility, street lights (some dead), trees, map bounds.
  See [Reference/README.md](Reference/README.md) for how the real map was translated.
- Parking lot with the player's van and a glowing extraction pad (visual only until a later milestone).
- Atmosphere: dark ambient light, exponential fog, dim moonlight, post-processing (vignette,
  film grain, desaturation, bloom).
- Procedural placeholder audio (no sound files): global low drone, positional machine hum at
  each tank, electrical buzz on faulty lamps.

### Phase 2, milestone 1 — Raycast interaction: verified

Verified by Chad on Windows (2026-10-21): the prompt appears on the breaker box and E switches
the facility lights off and on. Rebuilt scene and player prefab committed.

- `IInteractable` interface (`Scripts/Runtime/Interaction`): prompt text, whether it can be used
  right now, and what happens. Pickups, doors and the extraction point will implement it.
- `PlayerInteractor` on the player: a 2.5 m ray from the camera finds the interactable under the
  crosshair; E uses it. Walls block the ray. Raises an `Interacted` event for later systems.
- `InteractionPromptUI` on the player: crosshair dot that brightens on a target and a prompt line
  such as "[E] Cut the power" (dimmed with no key hint when the object can't be used right now).
  Built at runtime with Unity's built-in font, so no UI assets are needed.
- `LightSwitch` (first interactable): a breaker box on the outside wall, left of the facility
  entrance, that switches every ceiling lamp in the facility off and on (with a click, a moving
  lever, and the lamp fixtures going dark).
- The existing player prefab gets the two new components automatically when the project opens
  or the scene is rebuilt.

### How to verify Phase 2, milestone 1

1. Open the project. The Console should show no red errors.
2. Press Play. A faint dot is in the middle of the screen.
3. Walk toward the facility entrance. Left of the door is a grey box with a yellow handle.
   Look at it from within about 2.5 m: the dot brightens and "[E] Cut the power" appears.
4. Press E: click sound, handle flips, every lamp inside goes dark and the buzzing stops.
   The prompt now reads "[E] Restore the power". Press E again to turn them back on.
5. Step back, or look away: the prompt disappears. Press Esc: the prompt disappears too.

### Phase 3, milestone 1 — The Foreman (first monster): code complete, NOT yet verified in Unity

Written in a cloud session without a Unity Editor. Nothing below has been compiled or run yet.

- `MonsterBrain` (`Scripts/Runtime/AI`): a four-state machine. **Patrol** walks a route of points.
  **Investigate** walks to a noise or half-seen target and searches for 5 s. **Chase** runs
  (5.2 m/s; you walk at 3.5 and sprint at 6) and gives up after 4 s without seeing or hearing you.
  **Attack** has a 0.5 s wind-up, then one grab for 100 damage, which kills you.
- Sight: a 110° cone. It reaches 22 m if you're lit (flashlight on, or standing in a lamp's pool)
  and 7 m in the dark. Walls block it. Within 2.5 m it senses you in any direction. Awareness
  builds faster the closer you are.
- Hearing: the `Noise` channel. Walking steps carry 5 m, sprinting 13 m, landing a jump 9 m, the
  breaker box 25 m. Walls halve those distances. While it chases you, hearing your steps keeps the chase going.
- `RuntimeNavMesh` bakes the walkable area from colliders when the scene starts, using Unity's
  built-in AI module (no extra package and no baked asset to keep in sync).
- `MonsterPresentation`: positional heavy footsteps, a breathing loop, a screech when the chase
  starts, and eyes that glow amber, then yellow, then red. All sounds are procedural placeholders.
- Player: `PlayerHealth`, `PlayerFootsteps`, `DetectableTarget` (lit or dark; every player is a
  target, ready for co-op), and `CaughtScreen` (the view snaps to the monster, then a black
  "YOU WERE CAUGHT" screen, and the level restarts).
- `MonsterDebugHUD`: press **F3** in Play mode to see the monster's state, its awareness, whether
  it sees you, and the last noise it heard. Select the Foreman in the Scene view to see its view
  cones and path.
- `MonsterBuilder`: the graybox Foreman (tall, hunched, hard hat), with patrol points picked
  automatically from the facility layout. Menu: **Horror Game > Add or Replace Foreman in Open Scene**.
- 11 play-mode tests (`Tests/PlayMode/MonsterBrainTests.cs`) drive every state change in a bare
  arena. They need the Test Framework package (`com.unity.test-framework` 1.5.1, built into Unity 6),
  now listed in the manifest.

### How to verify Phase 3, milestone 1

1. Open the project and wait for the import. The Console should show no red errors.
2. Window > General > Test Runner > PlayMode > Run All. All 11 tests should pass in about a minute.
3. Open the Phase 1 scene, then run **Horror Game > Add or Replace Foreman in Open Scene** (it saves the scene).
4. Press Play, then F3. Walk into the facility. The Foreman should be in Patrol and walking his route.
5. Sprint near him (within about 13 m): he switches to Investigate and walks to where you were.
6. Let him see you with the flashlight on: screech, red eyes, Chase. Let him grab you: caught
   screen, then a restart.
7. Turn the flashlight off (F) in a dark spot and stand still out of his path: he should walk past
   unless he comes within about 7 m in front of you, or 2.5 m in any direction.
8. Commit the changed scene, player prefab and new materials (M_Monster*).

### Known issues / risks

- Light intensities, fog density and post-processing are first-pass values and may be too dark
  or too bright on your monitor.
- The town is a compressed block-out, not to scale.
- Monster tuning (speeds, sight ranges, noise radii) is first-pass. Lamp pools count as "lit" inside
  60% of a lamp's range, so if lamps get brighter or wider in the blockout work, the Foreman will see
  you from farther away in lit areas.
- Phase 2.2 (scrap, PR #2) is not merged yet, so being caught only costs you a restart.
- The Foreman only exists in the Phase 1 facility. The new match factory interior scene (PR #4) has
  waypoints ready for him, but he isn't in it yet.

### Side milestone: match factory interior (builds; not hand-tested)

Separate generated scene with a slice of the match factory (receiving room, machine hall, burnt dip
room, stairwell, Foreman's office) and a reusable pixel filter. See
[MatchFactoryInterior.md](MatchFactoryInterior.md). Compiles and builds in Unity 6000.0.58f2 (batch); not yet play-tested by hand.

### Next milestone

After the Foreman is verified: Phase 3 milestone 2, environmental events and randomised scares,
then the Foreman's "clock him out" banishing at the time clock.

Phase 2, milestone 2: collectible objects with configurable values (scrap items built on
`IInteractable`), then inventory and carrying capacity.

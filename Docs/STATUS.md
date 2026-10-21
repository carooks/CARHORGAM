# Project status

_Last updated: 2026-10-21_

## Current phase: 1 — Playable foundation

### Implemented

Verified by Chad on Windows (2026-10-21): the project opens, the scene generates and the player
can walk around. The other checks below have not been reported yet.

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
- Parking lot with the player's van and a glowing extraction pad (visual only until Phase 2).
- Atmosphere: dark ambient light, exponential fog, dim moonlight, post-processing (vignette,
  film grain, desaturation, bloom).
- Procedural placeholder audio (no sound files): global low drone, positional machine hum at
  each tank, electrical buzz on faulty lamps.

### How to verify Phase 1

1. Open the project (see the README). The Console should show no red errors.
2. Press Play. You start in the parking lot next to the van, facing the facility entrance.
3. Check: WASD moves, mouse looks, Shift sprints, Space jumps, F toggles the flashlight.
4. Walk into the facility, up the ramp onto the mezzanine, and around all rooms. You should not
   be able to walk through walls, props or the pond, or leave the map.
5. Listen: a constant low drone, a hum near the tanks, a buzz near flickering lamps.

### Known issues / risks

- Not yet confirmed: flashlight, jump/ramp/mezzanine, collisions, audio, lighting look.
- Light intensities, fog density and post-processing are first-pass values and may be too dark
  or too bright on your monitor.
- `ProjectVersion.txt` names Unity 6000.0.58f2; Unity Hub will offer to open with whichever
  Unity 6 version is installed. Commit the updated file after the first open.
- The town is a compressed block-out, not to scale.

### Next milestone

Phase 1 sign-off: run the remaining checks above, tune lighting, and commit the generated scene,
materials and settings from the Windows machine. Then Phase 2, milestone 1: raycast interaction
system (look at an object, see a prompt, press E).

# Project status

_Last updated: 2026-10-22_

## Current phase: 2 — Core gameplay

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

### Phase 2, milestone 2 — Collectible scrap: written, not yet verified in Unity

- `ScrapItem` (`Scripts/Runtime/Items`): an `IInteractable` with a display name, a value range
  (the value is rolled each time you press Play) and a weight. Prompt: "[E] Pick up Toolbox ($42)".
- `PlayerInventory` on the player: keeps the picked-up items (hidden, attached to the player),
  with total value and total weight. No capacity limit yet; that is the next milestone.
- `InventoryHUD` on the player: bottom-left list of carried items with their values and a green total.
- Six kinds of graybox scrap built from primitives in `ScrapBuilder` (Old Radio, Toolbox, Brass Bell,
  Gas Can, Teddy Bear, Candlestick), with shinier materials so the flashlight picks them out.
  Seven are placed on the facility floor at `$` cells in `FacilityBuilder.Layout`.
- Shared `HudFactory` helper for runtime-built HUD text, used by both HUDs.

### How to verify Phase 2, milestone 2

1. Pull the branch, open the project, then **Horror Game > Rebuild Phase 1 Scene** > Rebuild.
   The Console should show no red errors.
2. Press Play and go into the facility with the flashlight on. Small coloured objects lie on the
   floor: one between the first shelves on the left, several in the middle rooms.
3. Look at one: "[E] Pick up Old Radio ($31)" (name and price vary). Press E: it disappears with
   a soft click, and the bottom-left corner lists it with a green "Total".
4. Pick up a few more: the list grows and the total adds up.
5. Stop and press Play again: the same items have different values.
6. Commit the changed scene, `Player.prefab` and the three new `M_Scrap*` materials, then push.

### Known issues / risks

- Milestone 2 code was written without a Unity Editor and has not been compiled or run yet.
- Carried scrap can't be dropped yet and there is no carrying limit.

- Light intensities, fog density and post-processing are first-pass values and may be too dark
  or too bright on your monitor.
- The town is a compressed block-out, not to scale.

### Next milestone

Phase 2, milestone 3: inventory slots and carrying capacity (weight slows you down, a slot limit),
switching between carried items, and dropping them (G).

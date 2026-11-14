# Match factory interior (graybox)

A walkable slice of the old match factory, generated from code into its own scene:
`Assets/_Project/Scenes/MatchFactory_Interior.unity`. Primitives only, no asset packs.
Generator: `Assets/_Project/Scripts/Editor/FactoryInteriorBuilder.cs`.

## Build and play

1. Menu **Horror Game > Build Match Factory Interior**. It opens the new scene when done.
2. Press Play. You start in the loading yard under an orange door lamp, facing the half-open
   roll-up door.
3. Walk in. Right of the door is the main breaker with a red pilot light. **[E] Throw the main
   breaker** turns on the working hall lamps (some flicker). Until then it's moonlight and your
   flashlight (F).

Batch mode (Unity closed): `-executeMethod HorrorGame.EditorTools.FactoryInteriorBuilder.BuildBatch`.

## What's in it

| Area | Where (m, origin = hall SW corner) | Notes |
|---|---|---|
| Loading yard | X -3..62, Z -30..0 | Fenced, fogged, box trailer, dumpster |
| Receiving room | X 0..14, Z -12..0 | Roll-up door stuck 2.2 m open, pallets of cases, breaker |
| Machine hall | X 0..60, Z 0..20 | 7 m eaves, timber trusses, 9 match machines in two rows, line shafts with belts, roll stacks, carts, 3000 spilled matches |
| Collapsed bay | X 36..42 | Roof panel gone, moonlight shaft, wrecked machine, rubble |
| Dip room | X 0..16, Z 20..32 | Burnt out: soot walls, dip tanks with liquid, drums, fallen beams |
| Stairwell | X 10..16, Z 32..38 | Stairs end in rubble; chained door east |
| Foreman's office | X 52..60, Z 12..20, 3.5 m up | Stair along the north wall; desk lamp is on with no power |

One machine (row B, middle) hums even with the power off. `AI_Waypoints` holds 18 empty markers
along the routes for a future patrol; nothing reads them yet.

## Look

- Own post-processing profile `Settings/PostFX_Factory.asset` (dark grade: exposure +1, contrast 10,
  saturation -40, grain 0.5, vignette, slight chromatic aberration). Created once; tweak it by hand.
- `PixelationFilter` (Runtime/Rendering) drops the URP render scale to 0.5 with point upscaling
  while the scene plays, and restores it afterwards. Drop it into any scene for the same look.
- Fog exp² 0.03, ambient (0.05, 0.055, 0.06), moonlight from the south through the windows.

## Known gaps

- Verified 2026-11-14 on Windows (batch build, test renders, player build launches). Not yet play-tested by hand; the breaker starting OFF is unverified in Play mode.
- No doors open, nothing to pick up, no monster here yet.
- Rebuilding the Phase 1 scene resets Build Settings to Phase 1 only; rebuild this scene to re-add it.

# Match factory at night

The old match factory at night, generated from code into its own scene:
`Assets/_Project/Scenes/MatchFactory_Interior.unity`. It's laid out loosely after the aerial
photo and is not to scale, but it's much closer to the real size than the first slice.

The generator is the partial class `FactoryInteriorBuilder` in `Assets/_Project/Scripts/Editor/`:

| File | What it builds |
|---|---|
| `FactoryInteriorBuilder.cs` | Entry point, waypoints, fog, moonlight, post-processing and the player |
| `FactoryKit.cs` | Materials, walls with windows, stairs, railings, haze cones and lamps |
| `FactoryHall.cs` | Machine hall, roof, machines, line shafts, collapse, office, dip room, stairwell and breaker |
| `FactoryGrounds.cs` | Receiving room, warehouse, both brick wings and the courtyard |
| `FactoryProps.cs`, `FactorySyntyNames.cs` | Synty prop lookup with primitive fallbacks |

## Synty packs (bought, never committed)

The set dressing uses Synty POLYGON prefabs when they're imported under `Assets/Synty/`.
That folder is git-ignored because this repo is public. Import the packs (Police Station,
Town, Horror Mansion, Dungeon and so on) locally, under `Assets/Synty/`, then rebuild the
scene.

Without the packs the factory still builds. It's plainer, using primitive stand-ins or leaving
a prop out. The build log lists how many Synty props were placed and how many fell back.

## Build and play

1. Menu **Horror Game > Build Match Factory Interior**. It opens the new scene when done.
2. Press Play. You start in the gated drive between the two brick wings, facing the courtyard.
3. Cross the courtyard to the receiving room's roll-up door (west, under a sodium lamp). Right
   of the door inside is the main breaker with a red pilot light. **[E] Throw the main breaker**
   turns on the hall, office, warehouse and receiving lamps. Until then it's moonlight, the yard
   lamps and your flashlight (F).

Batch mode (Unity closed): `-executeMethod HorrorGame.EditorTools.FactoryInteriorBuilder.BuildBatch`.

## Layout

Coordinates are in metres. The origin is the machine hall's south-west corner, +X is east and
+Z is north.

| Area | Where | Notes |
|---|---|---|
| Drive / spawn | X 36..44, Z -40..-24 | Chained gate onto Route 94 behind you |
| West wing | X -10..36, Z -40..-24 | 3 storeys. Floor 1 is the packing room; floor 2 (stair in the SE corner) has the locker room and offices; floor 3 is sealed |
| East wing | X 44..96, Z -40..-24 | 3 storeys. Floor 1 is the canteen and locker room; upper floors are sealed |
| Courtyard | X -10..150, Z -24..0 | Trucks, a 40 m smokestack, dumpsters, weeds, sodium pole lamps, fences |
| Receiving room | X 0..14, Z -12..0 | Roll-up door stuck open, pallets, main breaker (starts OFF) |
| Machine hall | X 0..120, Z 0..24 | 20 bays, 8 m eaves, 21 match machines, line shafts, two collapsed bays at X 54..66 |
| Foreman's office | X 112..120, Z 14..24 | 3.5 m up a stair; desk lamp on with no power |
| Warehouse | X 120..150, Z -6..30 | 9 m high, pallet racking, three broken skylights, one collapsed rack run |
| Dip room | X 0..16, Z 24..36 | Burnt out |
| Stairwell | X 10..16, Z 36..42 | Dead end in rubble |

The scene exposes `FactoryInteriorBuilder.PlayerSpawn` and `NavBounds` (everything walkable)
for the monster code. `AI_Waypoints` holds 33 markers along the routes, on the ground, the
office platform and the west wing's second floor.

## Look

- **Lamps.** Enamel-shade lamps throw shadowed spot lights. The first 14 working ones cast
  shadows, which is what makes the pools of light read. Each lamp has an additive haze cone
  under it as a cheap fake volumetric.
- **Moonlight.** Moon shafts come through the collapsed roof and the warehouse skylights.
- **Post-processing.** The scene has its own profile, `Settings/PostFX_Factory.asset`. Its
  values are reapplied on every build, so edit them in code:
  - exposure +0.8, contrast 12, saturation -30
  - grain 0.35, vignette 0.38
  - bloom, slight chromatic aberration
- **Pixel filter.** `PixelationFilter` (Runtime/Rendering) runs at 75% render scale with point
  upscaling, softer than the first version's 50%.
- **Fog.** Exponential squared, density 0.022.

## Known gaps

- The expanded version has only been checked for code consistency so far. It hasn't been
  compiled or rendered in Unity yet.
- The Synty prefab names in `FactorySyntyNames.cs` are best guesses and need checking against
  the imported packs.
- The yard pole lamp lights sit at a fixed 7 m height, which may not match the Synty
  streetlamp's head.
- No doors open, and there's nothing to pick up.
- Rebuilding the Phase 1 scene resets Build Settings to Phase 1 only. Rebuild this scene to
  re-add it.

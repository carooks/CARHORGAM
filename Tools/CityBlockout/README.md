# Wadsworth city blockout

A real-scale (1 unit = 1 metre) street and building blockout of Wadsworth, OH, built from map data.
Roads are flat strips, buildings are plain extruded boxes. No facades, no detail: the art pass
comes later from the asset kit. This is for layout, scale and walking distances.

Default area: about 800 m east-west by 980 m north-south, centred on the match factory.
From the High/Broad/College intersection downtown in the north, down Route 94 past Pine and
Water to just past State Street in the south. The pond behind the factory is included.

Running the script also writes a top-down `out/wadsworth_preview.png` (not committed).

## What's in the model

| Object in Unity | What it is |
|---|---|
| `Ground` | One flat plane under the whole area |
| `RoadMain` | Route 94 (Main / High Street), 10 m wide |
| `RoadStreet` | Named streets, 7 to 9 m wide |
| `RoadService` | Driveways, alleys, parking aisles, 3 to 5 m |
| `Intersections` | Patches that hide seams where roads meet |
| `Parking`, `Water` | Mapped parking lots and the pond |
| `Bldg_0000` ... | One object per building, biggest first, walls + roof as two submeshes |

`Bldg_0000_Match_Factory` is the factory complex (raised to 14 m) and `Bldg_0001_Static_Building`
is the long building across Route 94. Landmarks are matched by location in the script's `LANDMARKS` list,
so add more there (a point inside the building, an optional height, a label). Named OSM buildings carry their name, e.g. `Bldg_0002_Central_Intermediate_School`.

`wadsworth_buildings.json` lists every building with its centre, oriented size, rotation,
height and outline, in the same coordinates. A later editor script can use it to swap boxes
for kit houses automatically.

## Coordinates

- Origin (0,0,0) = Route 94 centreline in front of the match factory (lat 41.02069, lon -81.72969).
- In Unity: +X east, +Z north, +Y up. Route 94 runs along Z at X ≈ 0. The factory is at X ≈ -100.
- The ground is flat. Real Wadsworth has hills; there's no elevation data in this.

## Open it in Unity (Windows)

The easy way, with nothing to copy:

1. Pull this branch and open the project. Wait for scripts to compile (no red errors in the Console).
2. Menu **Horror Game > Build Wadsworth Blockout Scene**. It reads
   `Assets/_Project/Data/Wadsworth/wadsworth_blockout.json` and creates
   `Assets/_Project/Scenes/Wadsworth_Blockout.unity` and `Assets/_Project/Meshes/Wadsworth_Blockout.asset`,
   using the project's existing materials, with colliders on the ground and buildings, invisible walls at the
   map edge, the same night lighting as Phase 1 (lighter fog) and the player standing on Route 94 facing the factory.
3. Press Play and walk. The Phase 1 scene is not touched.
4. Commit the new scene and mesh asset from your machine.

The OBJ route (if you'd rather have a model file): copy `out/wadsworth_blockout.obj` and `.mtl` into
`Assets/`, tick **Generate Colliders** in the Model tab, remap the materials in the Materials tab to the
project's `M_` materials, and drag it into a scene at (0,0,0). Commit it from your machine (OBJ goes through LFS).

## Rebuild or grow the area

Needs Python 3 (Pillow optional, for the preview image).

```
python Tools/CityBlockout/build_blockout.py                  # rebuild from cached data (also rewrites the Unity JSON)
python Tools/CityBlockout/build_blockout.py --half-width 600 --north 800 --south -900
python Tools/CityBlockout/build_blockout.py --fetch          # re-download map data first
```

`--north`/`--south`/`--half-width` are metres from the origin. The cached data in `data/`
covers about 1.2 km either side of Route 94, from about 2.7 km north of the origin to 1.15 km
south of it (far enough for Bergey Street and Terrace View). For anything bigger, widen
`FETCH_BBOX` in the script and run with `--fetch`. The OSM API refuses boxes over 0.25 square
degrees, so grow it in steps.

## Known limits (be honest with yourself about these)

- **Building outlines come mostly from Microsoft's machine-traced footprints** (441 of 466 in the
  default area). OpenStreetMap only has about 28 buildings mapped here. The traced outlines are good
  for houses but merge big complexes: the whole match factory is one blob.
- **Heights are rough.** Microsoft's estimates put the factory at about 7 m, so the script overrides it to 14 m
  (three storeys) as one flat-topped block. Most buildings come out 3.5 to 7 m (median 5 m). Expect to hand-fix landmarks.
- **Footprints are modern.** Buildings demolished before the imagery (e.g. Franklin Elementary)
  are missing, and anything built since the game's era is present.
- Road widths are guesses by road class. There are no sidewalks, curbs or lane markings.
- Roads and lots are cut at the map edge; buildings that cross it are left out.
- Tested in Unity 6000.0.58f2 in batch mode: it compiles, builds the scene, and test renders show solid
  buildings at sensible scale. Not yet walked in Play mode.
- The pond uses the existing night-water material, which is nearly black from above.
- Water has no collider, so you can walk across the pond.

## Credits (must ship in the game's credits)

Map data © OpenStreetMap contributors, available under the Open Database Licence (ODbL).
Building footprints: Microsoft Global ML Building Footprints, ODbL.

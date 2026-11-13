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

`Bldg_0000` is the match factory complex and `Bldg_0001` is the long building across Route 94
(The Static's). Named OSM buildings carry their name, e.g. `Bldg_0002_Central_Intermediate_School`.

`wadsworth_buildings.json` lists every building with its centre, oriented size, rotation,
height and outline, in the same coordinates. A later editor script can use it to swap boxes
for kit houses automatically.

## Coordinates

- Origin (0,0,0) = Route 94 centreline in front of the match factory (lat 41.02069, lon -81.72969).
- In Unity: +X east, +Z north, +Y up. Route 94 runs along Z at X ≈ 0. The factory is at X ≈ -100.
- The ground is flat. Real Wadsworth has hills; there's no elevation data in this.

## Import into Unity (Windows)

1. Make a folder `Assets/_Project/Blockout/Wadsworth/` and copy in `wadsworth_blockout.obj`
   and `wadsworth_blockout.mtl` (from `Tools/CityBlockout/out/`, or from the project files).
2. Click the `.obj` in the Project window. In the Inspector, **Model** tab:
   - Scale Factor: `1`. Tick **Generate Colliders**. Click **Apply**.
3. **Materials** tab: under *Remapped Materials*, point each slot at an existing project material
   so nothing renders pink in URP: `Ground` → `M_Ground`, `RoadMain`/`RoadStreet`/`RoadService`/`Parking` → `M_Asphalt`,
   `Building` → `M_Concrete`, `BuildingLandmark` → `M_FacilityWall`, `Roof` → `M_Roof`,
   `Water` → `M_PondWater`. Click **Apply**.
4. Make a new scene (File > New Scene > Basic (URP)), drag the `.obj` in at position (0,0,0),
   then drag `Assets/_Project/Prefabs/Player.prefab` in at about (8, 1, 0) (on Route 94,
   in front of the factory). Delete the default Main Camera. Press Play and walk.
5. Sanity check: a house should be roughly 3.5 times the player's height (about 7 m).
   If everything is tiny or huge, the scale factor is wrong; set it so houses are about 7 m.

Commit the `.obj`/`.mtl` from your machine (they go through Git LFS, which the cloud can't upload).

## Rebuild or grow the area

Needs Python 3 (Pillow optional, for the preview image).

```
python Tools/CityBlockout/build_blockout.py                  # rebuild from cached data
python Tools/CityBlockout/build_blockout.py --half-width 600 --north 800 --south -900
python Tools/CityBlockout/build_blockout.py --fetch          # re-download map data first
```

`--north`/`--south`/`--half-width` are metres from the origin. The cached data in `data/`
covers about 1.2 km either side of Route 94, from about 2.7 km north of the origin to 1.15 km
south of it (far enough for Bergey Street and Terrace View). For anything bigger, widen
`FETCH_BBOX` in the script and run with `--fetch`. The OSM API refuses boxes over 0.25 square
degrees, so grow it in steps.

## Known limits (be honest with yourself about these)

- **Building outlines come mostly from Microsoft's machine-traced footprints** (450 of 478 in the
  default area). OpenStreetMap only has about 28 buildings mapped here. The traced outlines are good
  for houses but merge big complexes: the whole match factory is one blob.
- **Heights are rough.** Microsoft's height estimates put the factory at about 7 m; the real
  wings are three storeys (about 14 m). Houses default to 6 to 7 m. Expect to hand-fix landmarks.
- **Footprints are modern.** Buildings demolished before the imagery (e.g. Franklin Elementary)
  are missing, and anything built since the game's era is present.
- Road widths are guesses by road class. There are no sidewalks, curbs or lane markings.
- Roads that leave the area keep their last segment, so a few stick out past the ground edge.
- Not yet checked inside Unity: generated and checked with a Python renderer only.

## Credits (must ship in the game's credits)

Map data © OpenStreetMap contributors, available under the Open Database Licence (ODbL).
Building footprints: Microsoft Global ML Building Footprints, ODbL.

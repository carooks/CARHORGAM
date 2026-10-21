# CARHORGAM

A small first-person co-op horror game (single-player prototype for now) set in a night-time
version of Wadsworth, Ohio. Inspired by the tension and low-poly look of Lethal Company.

- Engine: Unity 6 (6000.x), Universal Render Pipeline, C#
- Target: Windows PC
- Current state and next steps: [Docs/STATUS.md](Docs/STATUS.md)

## Opening the project (Windows)

1. Install **GitHub Desktop** (it includes Git LFS) and clone `carooks/carhorgam`.
2. In **Unity Hub**: Projects > Add > *Add project from disk* > pick the cloned folder.
   If Hub says the editor version isn't installed, choose the Unity 6 version you have and confirm.
3. Open the project. The first import takes a few minutes while Unity downloads packages.
   - If Unity asks to **enable the new Input System backends and restart**, click **Yes**.
4. On first open the project builds itself: it configures URP, generates
   `Assets/_Project/Scenes/Phase1_Graybox.unity` and opens it. The Console shows
   `[Horror Game] Built Assets/_Project/Scenes/Phase1_Graybox.unity`.
   If that didn't happen, use the menu **Horror Game > Rebuild Phase 1 Scene**.
5. Press **Play**, then click inside the Game view to capture the mouse.

| Action | Key |
| --- | --- |
| Move | W A S D (or arrow keys) |
| Look | Mouse |
| Sprint | Left Shift |
| Jump | Space |
| Flashlight | F |
| Release / recapture mouse | Esc / left click |

## Project layout

```
Assets/_Project/
  Scripts/Runtime/        game code (assembly HorrorGame.Runtime)
    Player/               input reader, movement, mouse look, flashlight
    World/                environment behaviours (flickering lights)
    Audio/                procedural placeholder sounds, ambient emitters
  Scripts/Editor/         editor tooling (assembly HorrorGame.Editor)
    ProjectBootstrap      first-open setup
    Phase1SceneBuilder    generates the graybox scene
    FacilityBuilder       facility interior from a text grid
    TownBlockoutBuilder   streets, houses, trees, pond around the facility
    RenderPipelineSetup   URP asset + post-processing profile
  Scenes/ Materials/ Prefabs/ Settings/ Meshes/   generated on first open
Docs/
  STATUS.md               implemented features, known issues, next milestone
  Reference/              real-world reference material (map of the area)
```

### Conventions

- All game content lives under `Assets/_Project`. Third-party assets go in their own top-level folder.
- Player components read input only through `PlayerInputReader`, never from devices directly.
  This keeps a clean seam for networking and AI-driven players later.
- The graybox scene is generated from code. Change the layout in `FacilityBuilder.Layout` or
  `TownBlockoutBuilder`, then run **Horror Game > Rebuild Phase 1 Scene**. Materials and the
  player prefab are only created when missing, so hand edits to them survive a rebuild.
- Binary files (textures, audio, models) are stored with Git LFS (see `.gitattributes`).

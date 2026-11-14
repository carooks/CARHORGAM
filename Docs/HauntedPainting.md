# Haunted painting (test level)

A cursed portrait you can step into: a pocket world a painter trapped something in. Inside is a
dusk field with a choir buried in it up to their necks, singing. The world only finishes painting
itself where you look. When the choir stops singing, something stands up in the unfinished paint.

## How to play it

1. In Unity: **Horror Game > Build Haunted Painting Test**. This generates
   `Scenes/HauntedPainting_Test.unity` and adds the paint filter to the URP renderer (switched off).
2. Press Play. You are in a shut-up parlor. Walk to the portrait on the far wall; the smeared
   face starts to move as you get close. Look at it and press **E**.
3. Inside: a dusk field, a farmhouse at the top of the path, three easels, and 28 heads buried in the grass.
   - The world paints itself in wherever you look. Turn around and it dissolves into wet paint behind you.
   - **While the choir sings, you are safe, and they show you the way.** Every head turns toward
     the nearest easel you have not burned (the frame, once they are all burned). The chord is in
     tune when you face that way and goes sour when you face away.
   - **Every 30 to 50 seconds, and every time an easel burns, they all stop at once and snap their
     heads to one spot** in the wet paint behind you. Something is standing there.
   - **It can only move where the paint is wet.** Follow the heads' eyes and look at it: it freezes.
     Look away for more than about a second and it comes. After about 11 seconds the singing starts
     again and it sinks back into the paint.
   - Press E at an easel to burn it. Every burn stops the song with a shriek and makes the thing faster.
   - The frame you came through will not open until all three easels are burnt. Then run back to
     it and press E to escape. Escaping breaks the curse: the portrait is left burnt.
   - If it reaches you, you die and the level restarts.
4. Exe: **Horror Game > Build Haunted Painting Test (.exe)** writes `Builds/HauntedPainting/HauntedPainting.exe`.

## How it works

| Piece | File |
| --- | --- |
| The portrait you press E on (wakes, empties, burns) | `Runtime/Painting/HauntedPainting.cs`, `Shaders/HauntedPortrait.shader` |
| Entering, leaving, atmosphere swap, burn count, frame lock, subtitles | `Runtime/Painting/PaintedRealm.cs` |
| "Paints only where you look": 64 compass directions, each 0 (wet) to 1 (finished) | `Runtime/Painting/PaintAttention.cs` |
| Oil-paint look: rotated, stretched Kuwahara filter plus wet underpainting, drips, dread | `Shaders/PaintedRealm.shader`, `Runtime/Painting/PaintFilter.cs` |
| The choir: singing, silences, heads that point the way | `Runtime/Painting/Choir.cs`, `Runtime/Painting/ChoirSinger.cs` |
| The thing that stands up when they stop (placeholder body, swappable) | `Runtime/Painting/PaintedEntity.cs` |
| Easels / anything you burn | `Runtime/Painting/BanishAnchor.cs` |
| The exit frame | `Runtime/Painting/RealmExit.cs` |
| Scene generator | `Editor/PaintingBuilder.cs` |
| Tests (4) | `Tests/PlayMode/PaintingTests.cs` |

- The realm is built in the same scene, 1 km east of the parlor. Entering is a teleport plus a
  switch of fog, ambient light, post-processing and filters, so no scene loading and the player
  keeps their state. Wrong Doors can use the same plumbing.
- The paint filter is a URP **Full Screen Pass** renderer feature named `PaintedRealm` on
  `URP_Horror_Renderer`. It is off except while you are inside. The pixel filter is switched off
  inside, because brushstrokes do not survive being rendered at half resolution.
- `BuildPortrait` and `BuildRealm` are public, so the portrait and its world can be dropped into
  a house scene later.

## Open decisions (Chad)

- **The choir is the entity** (Chad's pick). What stands up when they stop is a placeholder body
  (tall, long coat, face wiped off into a smear).
- **Paint tools** (throw white paint to erase, wet paint that slows it, paint your own door out)
  are not built yet. Next milestone, after this one is played.
- **Where the painting hangs** in the real town (a house in ring 1 is the obvious place).
- **Which of the 8 entities this is**, or whether it is a 9th.

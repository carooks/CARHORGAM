# Haunted painting: the buried choir (test level)

A painter cursed a canvas and trapped something in it. Step into the portrait and you are in a
pocket world you have to escape: a dusk field in thick fog, painted in heavy oil strokes, with a
choir buried up to their necks in the grass, singing.

## How to play it

1. In Unity: **Horror Game > Build Haunted Painting Test**. This generates
   `Scenes/HauntedPainting_Test.unity` and adds the paint filter to the URP renderer (switched off).
   Or play the exe: **Horror Game > Build Haunted Painting Test (.exe)** writes
   `Builds/HauntedPainting/HauntedPainting.exe`.
2. You start in a shut-up parlor. The smeared face in the portrait moves as you get close.
   Look at it and press **E**.
3. Inside:
   - **The world only finishes painting itself where you look.** Turn around and it dissolves into
     wet, unfinished strokes behind you.
   - **The song is the map.** Every head turns toward where you need to go next, and the chord
     swells and comes into tune when you face that way and sours when you face away.
   - **The chord is missing voices.** Four singers around the field have their mouths painted over
     in white. Press E on one to scrape the paint off and they join in. The song leads you from one
     to the next, all the way round the field.
   - **The thing in the painting hates harmony.** It gets angrier all the time, faster the fuller
     the chord, faster still while the choir is in tune, and in a burst every time you free a voice.
     When it boils over, the choir falls silent in fear and every head turns to where it stands, in
     the wet paint behind you.
   - **It does not chase.** It walks slowly to the last sound it heard (footsteps carry 5 m walking,
     13 m sprinting; scraping paint 8 m), listens, then wanders on. **Stand still and quiet and it
     passes you.** It only kills someone it reaches while they are still making noise. It can only
     move where the paint is wet: follow the heads' eyes and look at it, and it freezes. After about
     12 seconds it sinks away and the singing starts again.
   - **Four identical black churches stand in the fog; only one is real.** False doors open onto
     more field somewhere else, and the singing stops. The real one stays shut until the chord is
     complete; then the song leads you to it.
   - **The real church** is a long black nave with a head buried in every pew, the song deafening.
     Walk to the altar: every voice at once, the thing stands up behind the altar and the sound
     destroys it. You are back in the parlor and the canvas is burnt.
4. **F6** cycles the voice filter (off, half, full) so the raw synth voices can be compared.

## How it works

| Piece | File |
| --- | --- |
| The portrait you press E on (wakes, empties, burns) | `Runtime/Painting/HauntedPainting.cs`, `Shaders/HauntedPortrait.shader` |
| Entering and leaving, atmosphere swap, voices, churches, finale, subtitles | `Runtime/Painting/PaintedRealm.cs` |
| "Paints only where you look": 64 compass directions, each 0 (wet) to 1 (finished) | `Runtime/Painting/PaintAttention.cs` |
| Oil-paint look: rotated, stretched Kuwahara filter plus wet underpainting, drips, dread | `Shaders/PaintedRealm.shader`, `Runtime/Painting/PaintFilter.cs` |
| The choir: guidance, tuning, anger, silences, voice filter | `Runtime/Painting/Choir.cs` |
| One singer: voice, head, mouth, filters | `Runtime/Painting/ChoirSinger.cs` |
| A painted-over mouth you scrape free | `Runtime/Painting/MutedVoice.cs` |
| The thing (placeholder body, swappable) | `Runtime/Painting/PaintedEntity.cs` |
| Church doors | `Runtime/Painting/ChurchDoor.cs` |
| Scene generator | `Editor/PaintingBuilder.cs` |
| Tests (6) | `Tests/PlayMode/PaintingTests.cs` |

- **Voices** are synthesized in code (a held "ooh" on one chord note per singer). To use real
  recordings, drop a looping clip of someone holding one note (ideally A3, 220 Hz) into a
  singer's **Voice Clip** field; the game pitch-shifts it to that singer's note.
- **Voice filter:** an Audio Reverb Zone over the field gives the huge echo; each singer has a
  low-pass filter (sung from under the earth), a slow shared tape wobble, and distortion that
  rises with the thing's anger. The Choir's **Filter Strength** (0..1) scales all of it.
- **Co-op:** the tuning is averaged over every player. Split up and the chord sours: no guidance,
  but nothing to anger it either. Move together and you get guidance but draw it.
- The realm is built in the same scene, 1 km east of the parlor; the church interior sits 200 m
  north of the field. Entering is a teleport plus a switch of fog, ambient light, post-processing
  and filters, so no scene loading. Wrong Doors can reuse this.
- The paint filter is a URP **Full Screen Pass** renderer feature named `PaintedRealm` on
  `URP_Horror_Renderer`, off except while inside. The pixel filter is off inside.

## Open

- Swap primitives for Chad's local medieval / Abandoned Village asset packs (churches, fences,
  trees). The packs stay out of the repo.
- Paint tools (wet paint that slows it, throwing white paint) are not built.
- Where the painting hangs in town, and how it fits the 8 entities.

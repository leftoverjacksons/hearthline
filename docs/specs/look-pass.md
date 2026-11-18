# Spec: the look pass (M2)

Status: **draft 2026-11-18**, waiting for Travis's approval. Three questions are at the end.

## Goal

The whole game should look like one game, in the family Travis named: **Valheim, The Long Dark, Medieval
Dynasty**. What those share, and what DESIGN already asks for (*painterly realism*):
- simple, readable shapes;
- textures that suggest rather than show detail;
- mood carried by light, fog and colour.

Right now the land and sky have had a shading pass (ADR 0018). Everything people built is grey primitive
boxes, and the one style test (ADR 0022) went toward photo-scanned realism, which doesn't match.

The look should be judgeable **without playing**. Every step ends with the same set of fixed screenshots,
before and after, sent to Travis.

## Direction

- **Shapes:** chunky, slightly exaggerated, hand-made rather than procedural:
  - walls lean;
  - roofs sag;
  - planks are visibly planks;
  - nothing is a perfect box.

  Low to mid polygon count, with bevelled edges that catch light.
- **Surfaces:** painterly. Broad colour variation, brushy value changes, edges picked out lighter, little
  fine noise. Texture colour is held in `palette.json` as now, so one palette governs everything.
- **Light:** warm and low near home, with long shadows and golden evenings. Fog lies in hollows and along
  water, and the distance turns blue. Interiors are warm, and lit windows at dusk are the key image
  (DESIGN).
- **Palette:** saturated greens and golds near home; colder and greyer toward the Thin, with one pale
  blue-violet accent there (DESIGN).
- **The Thin:** wrongness in light (wrong shadows, as the Elder Works now has), stillness and that one
  accent colour. No creatures.

## Scope, in order

Each step is committed and judged from screenshots before the next.

1. **Style frames:** a tool (`StyleFrames`, Editor) that captures eight fixed shots.
   - The shots:
     - the homestead at dusk, with lit windows;
     - the homestead at noon;
     - a ruin in morning light;
     - the creek in morning fog;
     - the deep woods;
     - the Elder Works in fog;
     - a valley overview;
     - the player close up.
   - It captures the current state first, so there's a baseline.
2. **Light and grade** (no new models; the biggest change for the least work):
   - colour grading by time of day and by Thin level (a grading table or LUT per mood);
   - ACES tonemapping and bloom;
   - **fake volumetric fog:** layered height fog in hollows and over water, plus soft light shafts through
     the canopy (sprites and a screen-space pass; URP has no built-in volumetrics);
   - warm interior lights and window glow at the homestead, on at dusk;
   - the sky horizon fixed (it goes pinkish-purple in fog, ADR 0022).
3. **Painterly materials:**
   - `TextureForge` gets a painterly mode: big soft colour blotches, brush-direction strokes, and light
     edges from curvature.
   - All `HL_*` materials move to a shared **Hearthline/Painted** shader: wrapped diffuse, softened
     specular, edge highlight, and the existing wetness and fog.
   - The palette is retuned.
4. **A modelled kit** (Blender, through the Blender connection), replacing primitive boxes:
   - **Ruin pieces:** wall runs with broken tops, timber frames, gable ends, roof sheets, rubble, a door
     frame, window frames, a chimney. `RuinKit` builds from these instead of cubes.
   - **Homestead:** tent, cabin and farmhouse redone in the same hand.
   - **Elder Works:** standing stones, capstones and a proper mound mesh.
   - **Nature:** rocks and boulders, stumps and logs, and stylised trees to replace the blob trees (the
     leaf-card oak question in NEEDS is settled by this).

   Everything goes into `ArtSource/Greybox/*.blend`, exported to `Assets/Hearthline/Art/Greybox`, as the
   kit does now.
5. **Ground and grass:** a painterly ground colour map (dirt paths, worn yards, moss under trees), grass
   coloured to match, and a palette by area (golden fields, dark woods, a grey Thin).
6. **Characters:** the homesteader's materials moved to the painted shader so he sits in the world. His
   shape stays until a character direction is picked. This last step also unblocks M1's Mark visuals
   (ADR 0012 was waiting for an art direction).

**Out:**
- buying assets;
- animation;
- the gun and hunt feel (next pass);
- new gameplay;
- UI art.

## Performance

The game should stay above 60 fps at 1080p in the region on Travis's machine. `StyleFrames` logs frame times
for each shot, so a regression shows up in the same report.

## Acceptance

- `StyleFrames` captures the eight shots the same way every time (fixed seed, hour, weather and camera).
- After each step, before-and-after pairs are sent to Travis, and he says keep or redo.
- Every existing test still passes. The kit swap must keep collision and cover, so the PlayMode cover,
  place and combat tests are what guard it.
- No mixed styles left in any frame: no primitive boxes, no photo textures.

## Files I expect to touch

- **Editor:**
  - new `StyleFrames.cs`;
  - `TextureForge`, `GreyboxKit`, `RuinKit`;
  - `RegionSceneBuilder`, `SceneBuildKit`;
  - `HomesteadSceneBuilder` (or wherever the yard is built).
- **Art:**
  - new `Hearthline/Painted` shader;
  - `palette.json`;
  - `ArtSource/Greybox/*.blend` and the exported FBX;
  - the generated textures;
  - post-processing assets and LUTs.
- **Game:** `Atmosphere` (grade by hour and Thin), a light shaft and height fog renderer feature, and window
  lights.
- **Docs:** ADR 0027 (style direction, superseding ADR 0022's realism), DESIGN *Visual direction*, SETUP and
  NEEDS.

## Questions for Travis

1. **Which reference is closest to what you want?**
   - **(a) The Long Dark:** painterly, graphic, flat colour, very strong light.
   - **(b) Valheim:** chunky low-poly, crunchy textures, dramatic light and fog.
   - **(c) Medieval Dynasty:** nearly realistic.

   My recommendation is between (a) and (b), Long Dark surfaces on Valheim shapes. It's the most
   distinctive of the three, it's achievable with what I can make, and it hides the gaps (animation, faces)
   that (c) shows.
2. **Drop the photo-scanned direction from ADR 0022?** That means the free photo rocks pack and the photo
   grass, and moving the homesteader to painted materials. I recommend yes, because one style beats two.
3. **Make our own kit, or try free Asset Store packs?** Free stylised nature packs exist, but their licences
   don't allow them in a public repo. They'd be git-ignored, so the game would only build on your machine.
   I recommend making our own in Blender: it's consistent, it can be committed, and I can change it.

# 0028 — Light and colour: grade by hour and Thin, height fog, light shafts, home lights

Status: accepted (look-pass step 2, built 2026-11-18 after Travis said "go on"; judged from
docs/style-frames/3-light).

## Decisions

- **`ColourGrade`** (Game) drives its own volume with an in-memory profile (`HideAndDontSave`), layered over
  the scene's base grade (ACES, bloom, vignette). Neither play nor the style frames ever edit an asset.
  - **By hour:** temperature, saturation, contrast, exposure, and shadow and highlight tints. The look is
    Valheim's: saturated with a warm sun and cooler, teal-leaning shadows by day; gold toward evening; a blue
    dusk; cold and grey at night.
  - **Thin places:** drain the colour (saturation −45), cool it, and add a pale blue-violet in the midtones
    (DESIGN, *Palette*). Thin 1 (the deep coves) gets about a third of that, Thin 2 all of it.
  - **Grey weather:** costs saturation and contrast.
- **Height fog and light shafts** are one full-screen pass (`Hearthline/HeightFog`), installed on
  `PC_Renderer` before post-processing by `LookKit.InstallHeightFog`, which the region builder runs and which
  is safe to run again.
  - **Height fog:** densest at the valley floor (home height − 4 m) and thinning with height. It's integrated
    along each view ray from depth, so it pools in hollows and over creeks and glows toward the sun.
  - **Light shafts:** each pixel looks toward the sun on screen, and open sky seen through the canopy adds
    sunlight. Strongest with a low sun and when facing it.
  - The pass reads shader globals that `Atmosphere` sets: density by hour (thick at dawn, thin by noon),
    hollows and Thin, weather, and the Thin's hush. Scenes without an `Atmosphere` leave it at zero, which
    does nothing.
- **`HomeLights`:** the hearth fire (flickering), a lamp at the dwelling and one at the infirmary come up
  between 18:18 and 20:12, and go down at dawn. Each one only shows once its slot is built
  (`StructureView.ShowsBuilt`, which also works in the editor). This is DESIGN's key image of coming home at
  dusk to lit windows.
- **Light and colour changes:**
  - daytime sky and ground ambient raised so the forest floor isn't black;
  - dusk ambient and haze moved from orange-red to blue-violet so the warm lights stand out;
  - leaf-card tints brighter and yellower;
  - card crown shading (AO) 0.7 → 0.55.
- **`AreaVolume` registers in the editor too** (`ExecuteAlways`). Before this, editor tools saw no areas at
  all, so style frames couldn't show Thin or deep-woods fog. The registry was only filled in play.
- **Style frames** preview the home lights, and the dusk shot moves to 20:00.

## Cost

68–70 fps in editor Play Mode (RTX 2070) with or without the fog pass. The pass is about 24 depth samples
per pixel for the shafts.

## Known rough / for Travis to tune

- **Everything here is a feel value:** the grade curves on the `Atmosphere` object's `ColourGrade`; the fog
  density, falloff, cap, sun glow and shaft strength on `Atmosphere`; and the light strengths on
  `Home Lights`.
- The Elder Works mound and the kudzu are still flat colours (the materials step).
- Distant ridges wash out to pale grey in late afternoon. That's the distance fog, and it may want lowering.
- `ShadowsMidtonesHighlights` tints are clamped to 1 by `Gradient`, so they can only take colour away: teal
  shadows come from less red, not more blue.

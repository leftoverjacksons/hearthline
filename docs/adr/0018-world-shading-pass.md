# 0018 — World shading pass: textures, wind, water, sky

Status: proposed (2026-11-17), built overnight while Travis was away; every look value is a starting point
for him to tune. Stand-in art style, like ADR 0014.

## Decisions

- **Generated surface textures** (`TextureForge`, editor code): seamless 512 px detail and normal maps for
  bark, planks, stone, rock, concrete, corrugated tin, rust, canvas, leaves, grass and soil, written to
  `Assets/Hearthline/Art/Greybox/Textures`. The detail maps are near-white and multiply each material's
  palette colour, so `palette.json` still owns colour. `GreyboxKit` maps each `HL_*` material to a surface
  and a tiling per metre.
- **Kit models have UVs**: a per-face box projection at 1 UV unit per metre, done in Blender and exported
  with the FBX files (the source .blend is updated).
- **Foliage shader** (`Hearthline/Foliage`): leaves, grass, flowers, berries and kudzu sway by height
  above their pivot, with gusts rolling across the world and leaf flutter; wrapped diffuse and light
  through leaves; main-light shadows (which sway too), SH ambient, SSAO and fog; GPU instancing. Wind
  values are per material.
- **Water shader** (`Hearthline/Water`): transparent creek with depth colour, refraction of the creek bed,
  two layers of flowing procedural ripples, sun glints, a fresnel sky reflection and bank foam. Uses the
  depth and opaque textures already enabled in `PC_RPAsset`.
- **Sky shader** (`Hearthline/Sky`): horizon-to-zenith gradient, a long twilight with a sunset glow, sun
  disc and halo, drifting cumulus lit by the sun, and stars at night. `Atmosphere` passes the true sun
  direction and colour (`_HL_SunDirection`, `_HL_SunColor`) because URP's main light is the moon at night.
- **Ground**: the painted colour map gets URP Lit detail maps (grass blades and grain every 2 m), and
  ground cover is denser (about 24,000 instances).
- **Rendering settings**: SSAO strength 0.8 and radius 0.5 (was 0.4, 0.3) on `PC_Renderer`; sun shadow
  distance 110 m (was 50) on `PC_RPAsset`. Night ambient and moonlight raised slightly so night is playable.

## Known issues

- The ground detail is grass everywhere, so the dirt yard and path show faint grass streaks close up.
- Tree crowns are dark from directly below.
- The UV projection leaves visible seams on some round shapes (trunks).
- The kudzu drapes still read as bunches of grapes; the model wants a redo.

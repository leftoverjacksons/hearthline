# 0030 — Ground and grass

Status: accepted (look-pass step 6, built 2026-11-18; judged from docs/style-frames/6-ground).

## Decisions

- **The ground colour map** (`RegionSceneBuilder.GroundColour`) is repainted for the forest:
  - open ground is lush green where it's damp and gold where it's dry and on the balds;
  - the forest floor is warm leaf litter with patches of moss, with rust needle litter on dry ridges and deep
    green moss in the coves;
  - rock shows only on genuinely steep ground (the ridges are wooded to the top);
  - the Elder Works' pad is greyed.

  The close-up detail over it is soil grain, not grass blades, because the grass is real now.
- **Grass is cards**, like the leaves:
  - `TextureForge` adds `grassblades_albedo.png`, 120 tapering, leaning blades, dark at the root and light
    at the tip;
  - `build_kit.py` makes `Grass_Card_Tall`, `Grass_Card_Short` and `Grass_Card_Dry`: four crossed cards
    with vertex shading from root to tip, and normals pointing straight up so grass is lit like the ground it
    grows from;
  - the materials are `HL_GrassCard` and `HL_GrassCardDry`, cut-out, swaying with the foliage wind.
- **A grass scatter of its own** (`ScatterGrass`, its own `InstancedScatter`, 85 m draw distance, 25 m cells):
  - about one clump per metre wherever the ground is open, thinning into the forest's edge;
  - tall where it's damp, short in the yard and at the edges, gold where it's dry or high;
  - none on the road, the track, the water or the trampled middle of the yard;
  - knee to thigh high.

  The old `Grass_Clump` meshes are gone from the cover scatter. Wildflowers and ferns stay.

## Cost

About 4 fps in editor Play Mode: 62 at home with grass, 66 without (RTX 2070). Total headroom over 60 is
now small in the editor. A build runs faster, but trees, then grass, are what to cut first if needed (density,
draw distance).

## Known rough

- From high above, the clumps read as separate patches. At eye level they read as a meadow.
- A few old solid bushes (`Bush_A`, `Bush_B`) remain at forest edges, bright and blobby. They want the
  leaf-card treatment.
- Grass grows through ruin floors and around search spots. It's harmless, but untidy up close.

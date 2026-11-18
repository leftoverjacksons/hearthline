# 0029 — Painted surfaces and the model kit

Status: accepted (look-pass steps 4 and 5, built 2026-11-18; judged from docs/style-frames/4-painted and
5-kit).

## Decisions

### Painted surfaces (step 4)

- `TextureForge` paints over every surface pattern (`Painterly`):
  - the fine grain is blurred away;
  - the tone is snapped toward five soft bands;
  - big soft colour blotches, as if painted in a few broad passes;
  - brush streaks along the grain (vertical on bark, along the boards on planks, slanted elsewhere);
  - raised edges lightened and crevices darkened, from height curvature.
- Normal maps are about a third as strong (bump scale 0.7), so the painted albedo carries the detail.
- Non-metal materials are matte (smoothness 0.04).
- **Not done:** the separate `Hearthline/Painted` shader the spec proposed. URP Lit with painted textures and
  matte settings got most of the way, and a custom shader would have to re-earn shadows, SSAO, fog and
  wetness. Revisit only if the look calls for wrapped lighting on everything.

### Model kit (step 5)

- **Unit pieces** (`ArtSource/Greybox/build_kit.py`, stored in `region_kit.blend` as `hl_build_kit`): about
  1 m cubes, centred, so `RuinKit` scales them exactly as it scaled its boxes, and every ruin keeps its
  layout.
  - `Unit_Wall_Stone`: coursed fieldstone with a broken top.
  - `Unit_Wall_PlanksWeathered`, `Unit_Wall_PlanksDark`: separate boards with rails.
  - `Unit_Wall_Concrete`: chipped slab sections.
  - `Unit_Boards_*`: sagging boards for roofs, lofts, pews and docks.
  - `Unit_Tin`: corrugated sheet with a bent corner.
  - `Unit_Block_*`: bevelled blocks per material, for posts, beams, counters, pumps and chimneys.
  - `Unit_StandingStone`.
  - `Unit_Mound`: a long earth shell, closed at the back, open at the front.
- **`RuinKit.Piece`** picks the piece from what a part is and what it's made of: walls are wall runs, roofs and
  boards are boards or tin, standing stones are stones, and everything else is a block. It falls back to a box
  for anything the kit doesn't cover.
- **The Elder Works barrow** is one `Unit_Mound` shell over the stone hall. Its open face is filled with
  stones around the doorway, and the back closes itself, so the slabs, cores and back kerb are gone.
- **Replaced in place (same names, same footprints):**
  - `Rock_A`, `Rock_B`: faceted boulders with moss on top;
  - `Kudzu_Mound`, `Kudzu_Drape`: leaf-card mounds;
  - `Dwelling_Tent`: canvas sagging between A-poles, a back wall and a bedroll;
  - `Cabin`: notched log walls with door and window openings, board gables and roof, a stone chimney, a
    porch with a tin roof;
  - `Dwelling_Farmhouse`: two storeys of boards, eleven lit windows, a tin roof, a full-width porch, a
    chimney.
- `HL_TentCanvas` is now two-sided.

## Known rough

- Unit pieces stretch when scaled far from 1 m: a long wall run has wide boards, and the textures stretch with
  them (as the boxes' did).
- Not yet remodelled: the fire pit, kitchen, smokehouse, infirmary set, stores shed, fences, job board,
  salvage piles, the old bushes and the overpass. They're older kit models, not primitive boxes, so they fit
  better than the ruins did.
- The cabin and farmhouse were only checked in Blender. A new game starts with the tent, so the frames don't
  show them.

# 0027 — Style direction: Valheim-like, made by us, and a forest

Status: accepted (Travis, 2026-11-18; spec docs/specs/look-pass.md). Supersedes the realism direction of
ADR 0022.

## Context

Travis named Valheim, The Long Dark and Medieval Dynasty as the look, then picked **Valheim**. The built
world was grey primitive boxes, and the one style test (ADR 0022) had gone toward photo-scanned realism:
photo rocks and grass, and a realistic homesteader. Two styles at once is what DESIGN warns makes solo games
look cheap.

## Decisions

- **One style, Valheim-like:** chunky low-poly shapes, crunchy and simple textures, and dramatic light and
  fog carrying the mood. It fits DESIGN's *painterly realism*, and it hides the weak spots of an AI-built
  game (animation, faces) better than realism would.
- **The photo-scanned direction is dropped.** The Rocks FREE pack is no longer used by built scenes, and the
  style test scenes are kept only as a record. The homesteader moves to the painted materials (spec step 7).
- **We make our own models, above all for the environment** (Blender, through the Blender connection), so
  everything shares one hand and can be committed to the public repo. Asset Store packs only where making
  them is unreasonable, and git-ignored as before.
- **The land is forest.** The region is North Georgia and southern Appalachia, so you're under a closed
  canopy most of the time. Open ground is made by people, water and the odd bald. The generator,
  tree models, understory and performance work all follow from this (spec step 3).
- **The look is judged from fixed screenshots** (`StyleFrames`): the same eight shots after every step, so
  Travis can judge away from home.

## Consequences

- DESIGN's *Asset strategy* ("buy, don't build, for environments") no longer holds for the environment. It
  is updated to point here.
- Closed forest shortens sight lines. Hunting distances, enemy AI ranges and the deer's senses will want
  retuning (Travis tunes).
- NEEDS' "leaf-card oak vs blob trees" question is settled: both give way to the modelled trees.

## Built: the forest (spec step 3, done before step 2 at Travis's say, 2026-11-18)

- **The generator** makes forest the default:
  - base forest density about 0.66–0.9, thickest where it's damp;
  - thinned only on cliffs, on grassy balds above about 140 m, in a homestead clearing about 100 m
    across, in the old fields round each ruin, and on the Elder Works pad.
  - The northern mountain is now rounded Appalachian ridges running north-east, forested to the top.
  - `WorldMap.IsDeepCove` (forest > 0.82, moisture > 0.47, about 8% of the region) is the only place
    that counts as deep woods, which is Thin 1, silent and foggy. Everything else is ordinary woods, so the
    forest isn't all hollows.
  - Area tiles become a hollow when a third of their samples are cove.
- **The trees** (`ArtSource/Greybox/forest_kit.py`, stored in `region_kit.blend` as `hl_forest`):
  - species: white oak, tulip poplar, hickory, shortleaf pine (two shapes each), eastern hemlock and
    flowering dogwood;
  - each has a `_Far` version of about 20 vertices;
  - understory: rhododendron (two shapes), mountain laurel, sapling and stump.

  They're placed by site:
  - hemlock and poplar along the creeks;
  - poplar and oak in the coves;
  - pine and oak on dry ridges and in old fields;
  - oak and hickory elsewhere;
  - dogwood at the edges.

  Trees are about 6 m apart in and near the playable square, wider in the backdrop: 16,500 in all.
  Rhododendron thickets along the creeks are solid (capsule colliders), like a real laurel hell.
- **`InstancedScatter`:**
  - far versions past 120 m;
  - no shadows past 110 m (the pipeline's shadow distance);
  - cells outside the camera's view are skipped;
  - a lookup table makes building it fast.
- **Performance** (editor Play Mode, RTX 2070, 1975×719 editor window):
  - 47 fps before the culling, 63–69 after;
  - 101–120 with the trees switched off.

  A build runs faster than the editor. The trees are the biggest cost, so the next passes should watch it.
- **Tests:**
  - a new `WorldGenTests` check: the land is forest, with clearings where people made them and rare
    coves;
  - the deer-sight PlayMode test now finds a clear line of sight, because in the forest a trunk can block
    the straight one. That's the forest working, not a bug.

## Revised: leaf-card crowns (Travis, 2026-11-18: "the trees could not be lollipops")

The first forest had solid, faceted crowns, which read as lollipops next to Travis's Valheim reference. In
that reference, crowns are clusters of cut-out leaf sprays on visible limbs, with light glowing through.
So every tree now uses the ADR 0020 leaf-card method, generalised in `forest_kit.py` (`Builder`):
- **Limbs and cards:** limbs reach into one or more crown ellipsoids per species. Cards cluster at the limb
  ends, plus a shell that fills out the silhouette. Card normals bend outward from the nearest crown, crown
  shading is baked into vertex colour, and the bark is wrapped round each limb.
- **Species shapes:**
  - oak: one broad crown;
  - poplar: a tall, narrow crown;
  - hickory: two masses;
  - pine: five needle tufts on short limbs, plus dead stubs lower down;
  - hemlock: seven drooping tiers;
  - dogwood: flat layers.

  Rhododendron, laurel and saplings are card bushes too.
- **Far versions:** a few dozen big cards in the same silhouette, so distant trees stay ragged, not round.
- **Card materials:**
  - `HL_LeafCard` (oak, hickory, laurel), `HL_LeafCardLight` (poplar, dogwood, saplings),
    `HL_LeafCardDark` (rhododendron) and `HL_NeedleCard` (pine, hemlock);
  - `TextureForge` adds a needle spray, `needlecluster_albedo.png`;
  - light through the leaves is raised from 0.35 to 0.6 on all cards, for the sunlit glow.
- **Cost:** about 780 faces for an oak (330–650 for the others), 30–50 for the far versions. 66–71 fps in
  editor Play Mode, the same as the solid crowns.
- **Still to do in later steps:** the kudzu and old bushes are still solid blobs; the colour and the glow
  through the leaves belong to the light-and-colour step; the grass is the ground step.

# 0020 — Leaf-card trees

Status: proposed. One tree type (`Tree_Oak`) is converted as a test, for Travis to compare with the old blob trees.

## Context

The greybox trees (ADR 0014) have crowns made of a few solid, lumpy shapes. From a distance they read as trees;
up close they look like painted blobs. Most AAA games build crowns from **leaf cards**: many small quads, each
showing a cut-out spray of leaves, shaded so the whole crown reads as one soft mass.

## Decisions

1. **Cards with a cut-out texture.** `TextureForge` generates `leafcluster_albedo.png`: about 80 broad leaves on
   twigs, with alpha as the cut-out. Like the other kit maps it is near-white, and the `HL_LeafCard` palette
   colour tints it. It keeps its alpha coverage in the small mips, so trees don't thin out with distance.
2. **The foliage shader gains three switches**, all off by default so existing foliage is unchanged:
   - **Alpha clip:** cuts out the leaves in every pass, so shadows, depth and SSAO see the leaves, not the
     quads.
   - **Vertex AO:** the mesh's vertex colour (red) darkens the inside and underside of the crown. Back faces
     also keep the crown's outward normals instead of flipping.
   - **Received shadow strength:** a dense crown shadows itself almost completely, so the leaf cards take
     shadows at 55%.
3. **Built in Blender by script.** The script is the text block `hl_tree_oak` in `region_kit.blend`; tweak the
   numbers at the top and re-run, and it exports the FBX. It builds:
   - a trunk with a root flare and seven limbs, each with a side branch;
   - about 440 cards, clustered around the branch tips plus a shell that fills out the silhouette;
   - **bent normals** pointing outward from the crown's centre, so light falls on the crown as on a
     rounded shape, not on loose cards;
   - **baked crown shading** in vertex colours.
4. **The region uses `Tree_Oak` wherever `Tree_Broadleaf_A` was.** Same positions and seeds, so screenshots
   compare like for like. Broadleaf B and C, pines and the tall hollow tree are unchanged.

## Cost

- 620 faces per tree, against about 450 before, but cut-out pixels are drawn many times over where cards
  overlap.
- About a third of the region's 1641 trees are now oaks.
- Not measured yet. Before converting every tree type, check frame time in the dense woods and add a distance
  version (fewer, larger cards, or a flat impostor) if needed.

## Known issues

- A few clumps float just outside the crown at branch tips.
- An odd card seen edge-on reads as a thin streak.
- The leaf shape is generic (a broad, oval leaf). Reference photos would let it match real Southeastern trees:
  live oak with Spanish moss, sweetgum, tulip poplar, loblolly pine (docs/NEEDS.md).
- Trunk texture seams, as on the other trees.

## Feel values (for Travis)

- On the `HL_LeafCard` material:
  - wind 0.018 per metre, starting at 2.5 m;
  - AO strength 0.7;
  - received shadow 0.55;
  - cutoff 0.5.
- At the top of `hl_tree_oak`:
  - crown size;
  - card count and size;
  - normal jitter and bend;
  - darkest AO.

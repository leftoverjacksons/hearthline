# Grass: GPU blades around the player (approved 2026-11-18, built; ADR 0033)

## Why the current grass fails

Today's grass is about 400,000 clumps of 14 small cards, placed once by the region builder on a 1 m grid and baked into the scene.

- **Clumps read as objects.** One clump per square metre leaves gaps between clumps, so you see each one as a "thing on the ground". Lush grass is thousands of separate blades per square metre that you never see one by one.
- **Grass and ground are lit differently.** The cards have their own normals and shading, so even when they're the same colour they come out darker than the ground (the half-brightness problem). Lush grass is lit with the ground's normal, so the meadow and the earth under it read as one surface.
- **No distance strategy.** Clumps either draw or pop out at 25 m. In shipped games, far grass isn't geometry at all: the ground under it is painted the grass colour, and near grass thins into it.
- **It's frozen.** It's baked into the scene, so it can't be cut, trampled, burned or turned by the season, and it costs scene size and build time.

## What the games we like do

- **Ghost of Tsushima** (GDC, Wohllaib): every blade is made on the GPU each frame. Blades are a curved, tapered strip; Voronoi "clumps" share height, lean and colour so the field has patches; blades widen and thin out with distance; seen edge-on they're turned toward the camera so the field never looks sparse; wind is a scrolling noise field.
- **Valheim** (from fan breakdowns, not official): short blades whose colour comes from the ground under them, darkened at the root, and a terrain painted to match. The look is "a furry ground", not "plants on a ground".
- **Common to all:** root takes the ground colour, tip is lighter and a bit yellower; normals are bent toward the ground's normal (fully, with distance); fake AO by blade height, faded with distance; light shows through blade tips when backlit.

## The method I'd build

A `GrassField` in `Game/` that draws grass for a square around the camera every frame, with nothing baked into the scene.

1. **Where grass grows: a grass map.** The region builder bakes a texture over the world: density (0 under forest canopy, roads, water, buildings, rocks, search spots; up in clearings and meadows), height and dryness. It reuses the same rules the scatter uses now, including the collider check. Later the sim can write into it (cut, trampled, burned, season) through the Bridge.
2. **Placement on the GPU.** A compute shader walks a grid of cells around the camera (about 70 m), jitters a blade per cell, reads the grass map and the height, drops blades outside the camera view, and thins blades with distance. Survivors go into a buffer drawn with one indirect instanced draw.
3. **The blade.** A tapered strip of 5 to 7 vertices, curved by its lean and bend, about 0.25 to 0.6 m tall. Voronoi clumps set shared height, lean and colour. Edge-on blades are rotated a little toward the camera. Far blades get wider and fewer.
4. **Colour and light.**
   - Root takes the painted ground colour; tip lightens toward a dry-green from the grass map.
   - Normals blend toward the ground's normal (about half up close, all of it by 30 m), so grass and ground get the same light.
   - Height-based AO at the root only, fading out with distance.
   - Translucency at the tips when backlit.
   - Shadows received; casting only near the camera.
5. **Distance.** Past where blades end, the ground shader paints grassy ground with a fine noise in the same colour, so the meadow continues to the horizon with no visible edge.
6. **Wind and the player.** A scrolling noise field with gusts, tied to the weather's wind. Blades bend away from the player and animals.

**Cost:** my estimate is under 1 ms on a mid-range GPU for about 200,000 blades in view. That's a guess until I build it and measure. The baked clumps leave the scene, which shrinks it and speeds up rebuilds.

**Fallback:** if compute shaders aren't available, no grass is drawn and the painted ground carries the look.

## Files

- New: `Game/GrassField.cs`, `Art/Shaders/GrassBlades.compute`, `Art/Shaders/GrassBlade.shader`.
- Changed: `Editor/RegionSceneBuilder.cs` (bake the grass map, drop `ScatterGrass`), the ground shader (grassy far ground).
- An ADR (0033) to record the change from baked cards to GPU blades.

## Feel values left to Travis

All serialized on `GrassField`: density, draw distance, blade height and width ranges, clump size, root and tip colours, wind strength, how much the ground normal is borrowed.

## Acceptance

- From the eight style frames, the meadow reads as one surface with the ground: no single clumps, no stars, no hard edge where grass ends.
- Same brightness as the ground at the root.
- Grass never grows through rocks, logs, buildings, roads or water.
- The region stays at least as fast as today (about 62 fps in editor Play Mode at home).
- EditMode and PlayMode tests pass; a PlayMode test checks the field draws blades near the player and none on the road.

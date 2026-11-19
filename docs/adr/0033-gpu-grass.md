# 0033 — Grass made on the GPU around the camera

Status: accepted (Travis approved docs/specs/grass.md on 2026-11-18: "Build it"). Judged from
docs/style-frames/9-grass.

Supersedes the grass parts of ADR 0030 and 0032.

## Context

The baked grass (clumps of crossed cards, one per square metre, ADR 0030) read as "sprites stuck to the
ground". From above the crossed cards looked like stars, there were gaps between the clumps, and the cards came
out darker than the ground. Scattering smaller cards and tinting them with the ground's colour didn't fix it.

## Decision

Grass is no longer baked into the scene. `GrassField` (Game) builds it every frame for each camera, the way
Ghost of Tsushima does:

- **The region builder bakes two maps.**
  - `M2_Region_Heights.asset` holds the ground mesh's vertex heights.
  - `M2_Region_GrassMap.png` says where grass grows (r), how tall it is (g) and how dry it is (b). It uses the same rules as the old scatter: no grass on roads, tracks, water, steep rock or the hearth, or under anything solid, and none within 1.4 m of a search spot. Meadow edges are ragged with noise.
- **`GrassBlades.compute` places the blades.** It walks a grid of 8.5 cm cells that moves with the camera in whole cells, so a blade keeps its spot and looks the same as you walk.
  - It reads the two maps and puts each blade exactly on the ground's triangles.
  - Blades share height, lean and colour in Voronoi clumps.
  - Past 10 m the blades thin out and widen, and toward the 55 m edge they shrink into the ground.
  - Blades off screen are skipped.
  - The blades are drawn with one indirect draw.
- **`GrassBlade.shader` builds each blade.**
  - Shape: a tapering, curved strip that turns toward the camera when seen edge-on. It sways in the weather's wind and is pushed aside by the player.
  - Colour: the ground's colour at the root, lighter toward the tip, and gold where the grass is dry.
  - Light: the ground's PBR lighting, with the blade's normal bent toward the ground's. By 30 m blades are lit exactly like the ground, so the meadow and the earth read as one surface. Backlit tips glow.
  - SSAO: none on the blades. The root shade comes from the blade itself instead.
- **Every look and feel value is a serialized field on `GrassField`** for Travis to tune: density, reach, blade size, colours, wind and push.
- **No compute shaders, no grass.** The painted ground carries the look.

## Consequences

- The scene is smaller and the old grass instances are gone.
- Later, the sim can change the grass map through the Bridge (cut, trampled, burned, the seasons).
- Each camera that renders the region keeps about 19 MB of blade buffers.
- The grass cards (`Grass_Card_*`) are no longer placed. They stay in the kit for now.
- Grass casts no shadows by default (`castShadows`).

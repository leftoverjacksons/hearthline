# 0024 — The world is generated from a seed, land first

Status: accepted (2026-11-17: Travis asked for a natural land at the foot of a mountain, with water that
follows the land and roads laid afterwards).

## Decisions

- **`Hearthline.Sim.World.WorldGen` is pure, seeded C# in the simulation.** The same settings always give the
  same `WorldMap`, so a save only needs the seed. It is tested headless (`WorldGenTests`) and can be drawn as
  a map with `tools/headless/WorldPreview` (`dotnet run -c Release -- <seed> <out.bmp>`).
- **It builds in nature's order:**
  1. **land:** a rolling valley floor, foothills, and a ridged mountain to the north, then droplet hydraulic
     erosion;
  2. **water** read off the land: pits filled (priority flood), flow traced downhill (D8), catchment summed.
     Streams start where catchment passes a threshold, join into a river, and cut channels. The water
     surface never rises downstream;
  3. **plants:** moisture from flow and low ground; forest from patches, thinned on steep or high ground,
     thick by water, clear around home;
  4. **people:** a flat bench for the homestead at the origin (retried over a fixed seed sequence until
     water runs 30–110 m away), the old highway routed by A* (gentle grades, few and short water crossings,
     held to a corridor south of home), straight bridges wherever it meets water, a track home, and ruin
     sites on flat ground near the road and water.
- **Areas come from the map** (`WorldMap.AreaAt`): homestead, ruin, river crossing, road, creek, ridge, deep
  woods, woods, field. They are no longer hand-placed boxes.
- **Size:** a 1.2 km generated square at 2.5 m cells. The ~600 m middle is playable; the rest, the mountain
  included, is backdrop.

## Consequences

- The region scene is built from the map; changing the land means changing the generator or the seed.
- A persistent, unending world (generate the land in pieces as you travel, and keep what was made) is a
  continuation of this design, not a rewrite. It's an open question in DESIGN.

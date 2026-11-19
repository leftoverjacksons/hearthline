# Spec: trampled grass and desire paths

Status: **approved 2026-11-18, with household paths in v1** ("Include household paths"). ADR 0034.

## Goal

The land remembers where you walk, the way your body and your homestead remember what happened to them
(Marks).

- **In the moment:** grass mats down under your feet and your body, lies over in the direction you pushed through it, and stands back up over a few minutes. You can see where you, a deer or a reaver went through tall grass.
- **Over days:** routes you walk again and again wear in. First the grass there stays short and bent, then a bare dirt path shows. Paths you stop using grow back over weeks, more slowly in winter.

The homestead ends up with its own paths from cabin to well to garden, and a trail out to the hunting grounds you favour. No two families' land looks the same.

## Scope

**In:**

1. **Matting (presentation only, not saved).**
   - A trample map follows the player: 64 m square at 25 cm per texel, moving in whole texels.
   - Each frame, the player, deer and enemies stamp an oval at their feet into it. A stamp holds how hard the grass was pressed and which way the body was moving.
   - In the grass shader, matted blades lie over along that direction and are shorter.
   - The map fades back to standing over about 3 minutes (a setting).
2. **Wear (in the simulation, saved).**
   - **The data:** a `WearMap` in the world state. Each 1 m cell over the playable region holds a wear value from 0 to 1, stored sparsely: only worn cells are kept.
   - **Treading:** the Bridge sends a `Tread` command for each metre the player walks on foot. Each pass adds wear with diminishing returns: about 15 passes over a few days make a visible path, and about 40 make bare dirt.
   - **Growing back:** wear decays every in-game day. It decays fastest in spring and summer and barely at all in winter.
   - **All in the sim:** wear is seeded and deterministic and goes through `SimRandom` (though no randomness is planned). It is saved with the game.
3. **Showing wear (Bridge and presentation).**
   - The Bridge writes the wear map into a texture whenever it changes, at most once per in-game hour.
   - **In the grass:** worn cells get shorter, sparser blades that lean along the path, and none on bare dirt.
   - **On the ground:** the region's ground blends to trodden dirt where wear is high. This means moving the ground from URP Lit to a small `RegionGround` shader with the same look plus the wear overlay.
4. **Household paths (in the simulation, saved).**
   - The Bridge finds each job's route on the NavMesh, from the dwelling door to where the job is done (and an idle route for people without work). It registers the routes with a `SetHomeRoutes` command, and the world state keeps them.
   - Each in-game day, every living household member at home walks their route there and back: two passes over every cell it crosses. The head, anyone away on a trip and anyone laid up don't walk.
   - So the cabin grows paths to the woodpile, the hearth, the garden and the guard post, and a path fades when nobody does that work any more.
5. **Cheats (World tab):**
   - show wear as an overlay;
   - add wear along the last 100 m walked;
   - clear all wear;
   - age wear by 30 days.

**Out (later, if wanted):**
- Game trails worn by deer herds.
- Mud, footprints in snow or mud, and blood trails.
- Paths affecting travel speed or stealth (for example, tall grass hiding you).

## Rules

- Wear grows by `w += (1 - w) * 0.07` per pass, with at most one pass per cell per in-game minute, so standing still doesn't wear a hole.
- Each day, `w *= 1 - decay`. `decay` is about 0.03 in spring and summer, 0.015 in autumn and 0.003 in winter.
- Cells below 0.02 are dropped from the map.
- What shows:
  - from 0.25, the grass is shorter and bent;
  - from 0.55, dirt starts to show;
  - from 0.8, it's a bare path.

## Acceptance tests

- **Sim (EditMode):**
  - wear rises with passes and has diminishing returns;
  - repeated treads in one minute count once;
  - wear decays by season;
  - the same commands give the same map;
  - wear survives save and load;
  - small values are pruned.
- **PlayMode:**
  - walking 20 m through a meadow leaves matted blades behind you, and they recover;
  - after the "add wear" cheat, the ground's wear texture is non-zero along the route;
  - the region still runs at least as fast as now.
- **Style frames:** a homestead frame after 30 simulated days of daily trips shows paths from the cabin.

## Files expected

- **Sim:** `Sim/World/WearMap.cs`, `Sim/Systems/WearSystem.cs`, the tread command, save data, and tests in `Tests/EditMode/WearTests.cs`.
- **Bridge:** `Bridge/WearView.cs` (commands in, texture out).
- **Game:**
  - `Game/TrampleField.cs` (the matting map);
  - `GrassField.cs`, `GrassBlades.compute` and `GrassBlade.shader` (read trample and wear);
  - a new `Art/Shaders/RegionGround.shader`;
  - a test in `Tests/PlayMode/GrassTests.cs`.
- **Editor:** `RegionSceneBuilder.cs` (wire it up and switch the ground material), and `CheatPanel.cs`.
- An ADR (0034).

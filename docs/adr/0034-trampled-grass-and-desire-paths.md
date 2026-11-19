# 0034 — Trampled grass and desire paths

Status: accepted. Travis approved docs/specs/trampled-grass.md on 2026-11-18, with household paths in v1 ("Include household paths").

## Context

The land should remember where people walk, the way bodies and the homestead remember through Marks. There are two timescales:
- grass that mats down where something just went through;
- paths that wear in over days of use and grow back over weeks.

## Decision

**Matting is presentation only and isn't saved.**
- `TrampleField` (Game) keeps a 64 m map around the player at 25 cm per texel, moving in whole texels.
- The player, deer and enemies press an oval at their feet into it. The oval records how hard the grass was pressed and which way it was pushed.
- The map recovers over `recoverSeconds` (180 s).
- `GrassBlade.shader` lays matted blades over in that direction and lowers them.

**Wear lives in the simulation and is saved.**
- `SimState.Wear` holds a sparse map of 1 m cells, each with a wear value from 0 to 1.
- **The player:** `TrampleField` counts the player's steps, and `WearView` (Bridge) sends a `Tread` command each metre with the in-game minute. A cell counts at most once per minute.
- **Each pass:** `w += (1 - w) * 0.07`.
- **Growing back:** each day, `w *= 1 - decay`, with decay 0.03 in spring and summer, 0.015 in autumn and 0.003 in winter. Cells below 0.02 are dropped.
- **The household:** `WearView` finds each job's route from the dwelling door on the NavMesh and registers the routes with `SetHomeRoutes`. Each day, every member at home walks their route there and back (two passes per cell). The head, anyone on a trip and anyone laid up don't walk. A member without a job walks the idle route.
- No randomness is involved, so the same commands always give the same map.

**Showing wear.**
- `WearView` draws the sim's cells into a 720 m R8 texture, one texel per metre. It redraws on `WearChanged`, at most once a second.
- The texture is published as `_HL_WearMap`, along with the thresholds from `WearRules` (short 0.25, dirt 0.55, bare 0.8) and a dirt colour.
- **Grass:** `GrassBlades.compute` makes worn grass shorter and more bent, thins it once dirt shows, and drops it on bare ground. The blade roots there take the dirt colour.
- **Ground:** the region ground moves from URP Lit to `Hearthline/RegionGround`. It reproduces Lit's look (ground map, 2x soil detail, detail normal) under the same property names, so `WeatherView` still wets it. It blends to trodden dirt where wear is high, and the path's edge is frayed by the soil grain.

**Cheats (World tab):**
- show wear in red;
- wear the last 100 m walked (20 passes);
- clear all wear;
- age wear by 30 days.

## Consequences

- Saves grow with the number of worn cells. This is small: a household's paths are a few hundred cells.
- Routes come from the scene's NavMesh. If the homestead layout changes, `WearView` re-registers the routes on the next load. The old paths fade on their own.
- Paths don't yet change travel speed or stealth, and deer don't wear game trails.
- Feel values are serialized for Travis:
  - on `TrampleField`: size, recovery, oval size, press per kind;
  - on `GrassField`: `trample`;
  - on `WearView`: dirt colour;
  - on the `RegionGround` material: edge sharpness and breakup.

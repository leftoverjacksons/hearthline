# 0026 — Ruins and places: kinds in content, what's taken in the simulation

Status: accepted (spec approved 2026-11-18 with Claude's picks: docs/specs/ruins-and-places.md).

## Decisions

- **Kinds of place are content:** `Content/places/<kind>.yaml`, with name, area tags, Thin level, siting,
  search spot count, loot table, an optional hidden **find** (optionally behind a stat check), and an optional
  **rumour**. There are seven kinds: the burned farmstead, the fallen chapel, the filling station, the
  schoolhouse, the feed store, the fallen overpass and the Elder Works.
- **The generator describes sites; content chooses what stands there.**
  - `WorldGen` stays content-free. It gives each ruin site its road, water and home distances, its slope and
    rise, and a facing toward the road.
  - It also finds and levels a bench for the Elder Works on the mountain front: off the road, out of the
    gullies, and as far from home as the playable square allows.
  - `PlaceLayout.Assign` (in the sim, pure and deterministic) scores every site against every kind's siting.
    It takes the best pairs, and uses each kind once before any repeats.
  - The overpass and the Elder Works are `siting: { fixed: ... }`, placed by their own rule.
- **Places are identified by presentation and remembered by the simulation,** like gathering spots
  (ADR 0016).
  - Commands carry the place id and kind (`DiscoverPlace`, `SearchSpot`, `TakeFind`), and the sim keeps
    `SimState.Places` by id: discovered day, searched spots, find taken, and the last day the find was missed.
  - Ids are the kind for the first of a kind (`chapel`), then `chapel_2`, so they survive a rebuild.
- **Salvage never comes back** (Travis's choice of the options in the spec). A searched place stays searched
  for good, and the land empties over a lifetime, so heirs find what their parents stripped.
- **A checked find** always turns up at or above the needed stat. Below it, there's a seeded chance (25%, rising
  with the stat toward 75%), and a miss can be tried again the next day. Gear finds (the station's revolver)
  are made as gear with a ledger line, "Found at the filling station by ...".
- **Rumours at the job board** are a trip goal, like hunts. `FollowRumour` sets `SimState.Rumour`, and taking
  that kind's find ends it with a chronicle line. The HUD names the place with a distance and compass
  direction. A rumour is offered only while nobody has taken that kind's find.
- **Events can belong to a kind of place** with `when.place_any`, validated against `Content/places`.
  `EventContext.PlaceKind` comes from the smallest place `AreaVolume` around the player. The content-author
  subagent wrote seven place events.
- **Presentation:**
  - `RuinKit` (Editor) builds each kind from greybox boxes in its own frame, facing the road. Walls reach
    below ground so they sit on slopes, and their box colliders become cover automatically.
  - `PlaceSpot` (Bridge) is a search spot or the find, showing *full* or *gone through* from the sim.
  - `PlaceDiscovery` (Game) sends `DiscoverPlace` on first entry and fades the name in.
  - `ThinAura` (Game) makes Thin places hold their breath. Through `WeatherView.Hush`, the wind drops, the
    fog thickens and gusts stop, and at Thin 2 a low hum comes up.
- **The Elder Works** is a long barrow: a stone hall under an earth mound, its doorway toward home, ringed
  by standing stones.
  - **Wrong shadows:** each stone casts no shadow of its own. A leaning, shadow-only twin casts one a few
    degrees off from everything else's.
  - It has no find: the far end of the hall belongs to `the_humming_stone` event.
- **The hum** is CC0 (bretbernhoft, OpenGameArt), cut to a 15 s mono loop of 660 KB. Only the first
  6.5 MB of the 42 MB source was downloaded.

## Changes from the spec

- The Elder Works is at least **220 m** from home, not 350 m. The playable square is 600 m across with home
  at its centre, so 350 m only fits in a corner, and the mountain front there is too steep.
- The Elder Works has no find (see above).

## Known rough

- Everything is greybox boxes. Nothing has been checked by eye yet, so a wall may clip a slope or a spot
  may sit awkwardly.
- The cheat panel's place list reads the scene's `AreaVolume`s, so it only works in the region scene.

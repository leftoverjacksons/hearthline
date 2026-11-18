# Spec: ruins and places (M2)

Status: **approved 2026-11-18** (Travis, with Claude's picks: salvage never comes back, rumours in this spec, the hum downloaded). **Built** (ADR 0026): `PlaceTests`, `PlacePlayTests`, seven place events. Changes from this draft are listed in the ADR.

## Goal

The land should have places worth walking to. Right now the generator marks five ruin sites. The scene drops
rubble, a kudzu mound and one scrap pile at each, and gives them a name from a list, so there's no reason to
go to one rather than another. This spec gives each place:
- a kind;
- a shape you recognise from a distance;
- things to search, which stay searched;
- stories that only happen there;
- for the strangest ones, the Thin.

It also adds the region's **Elder Works** site. DESIGN, *World structure* asks for one in the vertical slice,
and the existing `the_humming_stone` event already wants it.

All of this serves the tone of the land (DESIGN, *Exploration*): wandering a lonely landscape of nature and
ruins. Most visits are salvage, sign and quiet; a few are strange; very few are dangerous.

## Scope

**In:**
- **Place kinds in content** (`Content/places/`):
  - the burned farmstead;
  - the fallen chapel;
  - the filling station;
  - the schoolhouse;
  - the feed store;
  - the fallen overpass (already built, now a kind like the rest);
  - the Elder Works.
- **The generator assigns a kind to each site,** seeded, with each kind sited where it makes sense:
  - the filling station by the highway;
  - the farmstead on flat open ground near water;
  - the chapel on a rise.

  It also places one Elder Works site on a bench of the mountain front, far from home.
- **Places in the simulation:** discovered or not, which search spots have been searched, and whether the
  place's one good find has been taken. All of it is saved.
- **Searching:** search spots inside each place roll that place's loot table through `SimRandom`.
  - Each spot can be searched once.
  - Every place hides one **find**: a better item, in the hardest spot to reach or behind a skill check.
- **Events tied to places:** a new trigger condition `place_any: [chapel, filling_station]`. A content
  subagent writes six to eight place events.
- **Discovery:**
  - The first time you enter a place, its name appears on screen ("The fallen chapel").
  - The chronicle notes it ("Ada first came to the fallen chapel").
  - It counts as known from then on.
- **Greybox ruins by kind,** built by a `RuinKit` from greybox parts:
  - broken walls with gaps and window holes;
  - a collapsed roof;
  - the chapel's fallen steeple;
  - the station's canopy and pumps;
  - the overpass as now;
  - the Elder Works as a low mound ringed with standing stones, with a sunken doorway into a short buried hall
    (one interior room, built into the hillside).

  Walls give cover, so a ruin is also somewhere a rare ambush can happen properly.
- **Cheats:**
  - go to a place;
  - discover all places;
  - reset a place (unsearched, find back);
  - start a place's events.

**Out:**
- real art; everything stays greybox (ADR 0012);
- doors, locks, traps and interiors beyond the Elder Works hall;
- other scavengers competing for the salvage;
- a map screen (places are found by walking; a map comes with the travel UI);
- Calvary Ford, which has its own spec later;
- camps, shrines and strangers' homesteads, the other points of interest in DESIGN, which come later on the
  same system.

## Simulation

- **Content:** `Content/places/<kind>.yaml`.

  ```yaml
  id: chapel
  name: the fallen chapel
  area_tags: [ruin]            # added to the place's area, for events
  thin_level: 0
  siting: { slope_max: 0.15, rise: true, road_max: 80 }
  search_spots: [3, 5]         # how many, min and max
  loot: chapel_salvage         # loot table for each spot
  find:
    item: old_coin
    count: [4, 9]
    check: { stat: tracking, difficulty: 2 }   # optional; no check = always found in the find spot
    text: "Under the altar step, a tin box someone meant to come back for."
  ```

  - The Elder Works has `thin_level: 2`, the area tag `elder_works`, and siting `{ mountain_bench: true,
    home_min: 350 }`.
  - New loot tables, one per kind, with salvage that fits the place:
    - the chapel: candles, lamp oil, a little coin;
    - the station: scrap, wire, oil, tools;
    - the farmstead: tools, cloth;
    - the schoolhouse: paper, a book;
    - the feed store: cornmeal, sacks, seed.

    New items are added where needed (candles, a book, tools, seed).
- **World generation:**
  - `WorldMap.Places` replaces `RuinSites`. Each place has an id, a kind, a position and a facing (toward
    the road).
  - The generator scores each site against each kind's siting and assigns kinds, so no kind appears twice
    before every kind has appeared once. It's seeded and stays the same after a load.
  - It adds the Elder Works site.
- **State:** `SimState.Places`, keyed by place id. For each place:
  - the day it was discovered;
  - which spots have been searched;
  - whether the find has been taken.
- **Commands:**
  - `DiscoverPlace { PlaceId }` emits `PlaceDiscovered` and adds a chronicle line.
  - `SearchSpot { PlaceId, Spot }` rolls the place's loot into the pack and emits `SpotSearched`. A second
    search of the same spot does nothing.
  - `TakeFind { PlaceId }` makes the skill check if there is one, then gives the find and emits
    `FindTaken`. If the check fails, you can try again another day.
  - `ResetPlace` is a cheat.
- **Events:**
  - `EventContext.PlaceKind`, and `when.place_any`.
  - The place's own area tags and Thin level flow into the context as they do today.
  - Content validation checks that `place_any` names real kinds.
- **Salvage doesn't come back on its own** (see question 1).

## Presentation

- **`RegionSceneBuilder`** builds each place from its kind with `RuinKit`. It also sets:
  - an `AreaVolume` per place, with the place's name, tags and Thin level;
  - search spots as `Interactable`s, with the verb *Search*;
  - one find spot.

  A searched spot looks rummaged: the pile is lower and its lid or tarp is pulled aside. It stays that way
  after a load, because it reads the sim.
- **The find spot** is the hardest to reach in each place: the chapel's altar step, the station's
  back-office safe, the hayloft over the farmstead's half-standing barn, and the Elder Works' far end of the
  hall. If there's a check, the prompt says what it needs ("Tracking 2: something was hidden here").
- **Discovery:** the place name fades in at the top of the screen the first time you enter, then the HUD's
  place line shows it as now.
- **The Elder Works feels wrong, not monstrous** (DESIGN, *Art*):
  - no birds and no insects (already true in Thin places);
  - the wind drops;
  - the fog thickens slightly inside the stone ring;
  - the standing stones cast their shadows a few degrees off from everything else's, using a second
    shadow-only light on a layer;
  - a low hum near the hall (see question 3).
- **Tuning:** all distances, sizes and the hum's volume are serialized for Travis to tune.

## Acceptance tests

- **Sim (EditMode, headless):**
  - the same seed gives the same places, kinds and positions;
  - every kind appears once before any repeats;
  - kinds respect their siting (the station near the road, the Elder Works on the mountain front and at
    least 350 m from home);
  - searching a spot gives loot once, and a second search gives nothing;
  - search state and the taken find survive save and load;
  - a find with a check can fail and be retried another day;
  - `DiscoverPlace` emits once and writes the chronicle;
  - an event with `place_any` fires only at that kind;
  - content validation covers places, loot references and `place_any`.
- **PlayMode:**
  - every place in the region has an area and its search spots;
  - walking into a place discovers it and shows the name;
  - searching a spot puts loot in the pack, and the spot looks searched, also after a reload;
  - the Elder Works is a Thin place: no birds, no birdsong, and the HUD shows Thin;
  - the find can be taken once.

## Files I expect to touch

- **Sim:**
  - `World/WorldGen.cs` (places and the Elder Works site);
  - new `Content/PlaceDefs.cs` and `Systems/PlaceSystem.cs`;
  - `SimState`, `ContentDatabase`, `ContentLoader`, `DomainEvents`, `EventDefs` and `EventReader`
    (`place_any`), `EventContext`, `Storyteller` (`WhenHolds`).
- **Content:**
  - `Content/places/*.yaml`;
  - new loot tables and items;
  - `vocab.yaml`;
  - six to eight events, written by the content-author subagent;
  - `docs/content-schema.md`.
- **Editor:** `RegionSceneBuilder.cs`, new `RuinKit.cs`.
- **Game:** search and find interactions, the discovery title, and `CheatPanel` (World tab).
- **Tests:**
  - `PlaceTests` and additions to `WorldGenTests`;
  - `PlacePlayTests`;
  - `TripFlowTests`, if place names change.
- **Docs:** ADR 0026, DESIGN (a line on places in *World structure*), SETUP and NEEDS.

## Questions for Travis

1. **Does salvage come back?**
   - **(a)** Never: a searched place stays searched for good, and the land empties over a lifetime.
     I recommend this. It fits the record-keeping idea, and your heirs would find the places their parent
     stripped.
   - **(b)** Slowly: one spot refills per season, as if others drift through.
   - **(c)** Never, but each new generation finds new places farther out, which ties into the endless-world
     question.
2. **A salvage goal at the job board?**
   - Hunts start at the job board. A *Rumours* section could name a place and its find ("They say the
     filling station's safe was never opened") and set it as the trip's goal, like Crookhorn.
   - It's small, and it gives the drift loop its "salvage site" goal (DESIGN, *The drift loop*). In this
     spec, or the next?
3. **The Elder Works hum:** OK to download one more CC0 sound (a low drone or hum) from OpenGameArt, under
   the same rules as the weather sounds? Without it, the place is silent, which is also eerie.

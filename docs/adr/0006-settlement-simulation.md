# 0006 — Settlement simulation, townsfolk, reputation decay and grudges (first pass)

Status: accepted as defaults, built on request without spec review (2026-11-16). Numbers are first
guesses in `WorldRules`.

## Decisions

- **Four values per settlement**, 0..100: prosperity, safety, strangeness, openness, as in the design.
- **Influence, not control.** Nothing sets a value. Events (`settlement:` outcome) and presentation
  (`InfluenceSettlement`) add a push that is stored and applied when the season turns. One season can
  move a value by at most 15; excess is discarded, not carried over. A value nobody pushed drifts 1
  toward the settlement's baseline.
- **Services** (doctor, gunsmith, saloon, shrine, school, telegraph) open when their thresholds are met
  and close when a value falls 5 past a threshold, so a town at the edge does not flicker. Towns can
  decline.
- **One home settlement.** Events that do not say where they are refer to it.
- **Townsfolk** are ordinary characters with a settlement and a role. They are created at new game from
  a separate random stream (so adding residents to content does not reshuffle everything else in a seed),
  they age and die like anyone, and when one dies a child with the same surname takes over the role and
  inherits the parent's memories of the family.
- **Memories** are short texts an event leaves on a person (latest 8 kept). Nothing reads them yet except
  the cheat panel; they are there for dialogue and later events.
- **Reputation** moves 1 toward zero each season.
- **Grudges** are per faction, belong to the household and so pass to heirs, do not decay, and cap
  reputation with that faction at -25 until settled. Events can be conditioned on them, which is how a
  band wronged by a grandmother still hunts the family.
- **A visibly Changed household member** (reaction tag `changed`) adds 1 strangeness per season to the
  home settlement.

## Not done

- The automatic inputs the design lists (trade volume, harvests, raids, plague, patrols) do not exist as
  systems, so today only events, commands and Changed residents move a town.
- Openness does not yet change how townsfolk treat Changed family members; it only gates events.
- Townsfolk cannot marry into the family, and there are no personal (per-person) grudges.
- Only Calvary Ford is defined. Service thresholds give it more than the two growth steps M5 asks for.

# 0015 — Events on the road

Status: accepted (2026-11-16). M2 step 2.

## Decisions

- **Presentation decides when the storyteller gets a turn; the simulation decides what happens.**
  `RoadEvents` sends `StorytellerTick` with the player's `EventContext` (area tags, place name, Thin level,
  distance from home, time of day) every `metresPerTurn` walked or `hoursPerTurn` game hours while away,
  after each night in camp, and every `hoursPerTurnAtHome` at the homestead (home events). The pacing
  values are serialized for tuning. The storyteller's own budget, threshold and chance (ADR 0005) still
  decide whether anything fires, so a turn is an opportunity, not an event.
- **One panel for every event.** The beat text with names cast in, available choices as buttons,
  unavailable ones greyed with the reason. A short summary follows the event's end, built from the
  domain events raised while it ran: items (into the pack on a trip, the stores at home), marks with their
  origin text (which names the place), reputation, people joining, gear taken, injuries and deaths.
- **Ambushes are reported, not fought,** until M2 step 5 stages them.
- **Time waits for events.** The clock already stops while a panel holds the player; days the simulation
  refuses during an event are owed and paid as soon as it ends, even while the summary is still up.
- The cheat panel can start any event "here" (with the player's real context) or give the storyteller a turn.

## Not done

- Events are text panels; nothing is staged in the world (a stranger by the creek is not standing there).
- No sound or visual cue that something is about to happen.

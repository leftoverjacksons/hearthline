# 0008 — Trips: setting out, the pack, and coming home (first pass)

Status: accepted as defaults, built on request without spec review (2026-11-16). Numbers are first
guesses in `TripRules`.

## Decisions

- **A trip is state.** `SetOut` takes the head, up to two companions (aged 13 or more) and a pack drawn
  from the stores; `ReturnHome` ends it and unloads the pack. `SimState.Trip` is null when everyone is
  home.
- **Two inventories.** On a trip, everything that gives, takes or checks items (events, loot, trade, the
  give-item cheat) uses the pack. Things that happen at the homestead (job yields, food, theft, building)
  use the stores. What is at home cannot be spent on the road, and what is on the road is lost if the
  head dies with nobody left to carry it.
- **Carrying.** A pack cannot be heavier at departure than the party can carry (30 per person, scaled by
  each one's carry_capacity, so marks matter). Loot may push the party over; `Overloaded` is then true
  and "danger" events become 1.5 times as likely. Presentation should slow an overloaded party.
- **Spoilage.** Items tagged `perishable` lose a quarter of the stack (at least one) per day in the pack,
  and half the stack per season in the stores unless the homestead has any preserving (drying rack,
  kitchen, smokehouse). This gives the preserving total its first consequence.
- **Seasons turn with the calendar.** Births, natural deaths, the home report, food, morale and the
  town's changes now run whenever a day advance crosses into a new season, including while the party is
  away. `PassSeason` is the player choosing to stay home until then, and is refused on a trip.
- **Companions are away.** They do not work at home, do not eat from the stores, and are the default
  party for events.
- Building and home treatment wait until the head (or the patient) is home.

## Not done

- A trip that lasts "a few days" does not by itself end the season, as was floated. The player comes
  home, sees the last report, and chooses when to pass the season. If a trip should always close a
  season, `ReturnHome` can call the same turn; that is a one-line design decision for Travis.
- Time of day, distance and where the party is are still supplied by presentation, not tracked here.
- The party does not eat on the road; hunger while travelling is not modelled.
- Horses, pack animals and the stable's carrying bonus.

# 0005 — Event engine and storyteller (first pass)

Status: accepted as defaults, built without spec review (2026-11-16).

## Decisions

- **Schema** follows the example in the design doc, with these changes: `recruitable` became `recruit`
  (the person joins at once, as Sworn); wounds come from a separate `hit` outcome; `trait`, `flag_set`,
  `flag_clear`, `chronicle`, `goto` and `intensity` were added; `traits_any` on a generated person
  became `traits` (they get all of them, and they must be real traits).
- **One event at a time.** The active event (event id, beat, cast) is in the save, so a game can be saved
  mid-event. Time cannot advance while an event is open.
- **Generated people are real characters.** They stay in the state after the event with their opinion of
  the head, so they can be met again or recruited. They are not household members.
- **`relationship`** changes the cast member's opinion of the head of household (−100..100).
- **Reputation and inventory belong to the household**, so heirs inherit them. Reputation is clamped to
  −100..100. Carried weight, spoilage and item history (the ledger, heirlooms) are not modelled.
- **Ambush is a request.** The sim emits `AmbushRequested` with an encounter id; presentation stages the
  fight and reports hits back with `InflictHit`. The event does not wait for the fight.
- **Storyteller.** Presentation calls `StorytellerTick` with where the party is. Each tick adds to a
  budget (more in the Thin and far from home). Over a threshold, there is a 50% chance per tick that an
  event fires, chosen by weight among those whose `when`, cooldown and cast fit. After an event the budget
  restarts below zero, further below for higher intensity, so events never come back to back and big ones
  buy a longer quiet. Events tagged `danger` are less likely while someone in the party is hurt.
- **Text tokens** are only names. Writing marks and history into narration ("her bad hand aching") is not
  done; it needs pronouns and per-mark phrases.

## Not done

- Faction heat, recent player choices and time since the last major event are not storyteller inputs.
- Reputation decay, grudges, settlement values and persistent townsfolk (the world simulation section).
- Event chains across days and generational callbacks, beyond what flags allow.
- Content: 14 events of the 20 the prototype milestone asks for. They cover every outcome kind.

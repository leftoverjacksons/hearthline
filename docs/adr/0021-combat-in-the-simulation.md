# 0021 — Combat: fight state in the simulation, the fight itself in presentation

Status: accepted (spec approved 2026-11-17). Build step 1 of `docs/specs/combat-and-ambush.md` is done.

## Context

M2 needs ranged combat and an ambush. Fights are real-time: aiming, bullets, enemies moving to cover. But
everything a fight changes must outlast it:
- wounds and Marks on both sides;
- spent ammunition, and gun wear and jams;
- a line in the gun's history;
- grudges and memories;
- loot.

The architecture rules say the simulation owns persistent state and presentation only sends commands.

## Decision

- **The sim holds the fight's state** (`SimState.Encounter`):
  - which enemies are in it: real `Character`s, so wounds, Marks, memories and death work on them;
  - each enemy's gun (a real `GearItem` with condition, loaded rounds and a jam flag), spare ammunition
    and state (fighting, down, fled, surrendered).
- **The sim decides everything that has a rule.** It generates the enemies (seeded); fires and reloads
  (`FireWeapon`, with jams rolled from the gun's condition; `ReloadWeapon` from the pack, stores or the
  enemy's supply); turns a hit into a wound (`ShotHit` takes the hit zone, distance and gun, and the sim
  picks the severity); breaks the group on morale (`GroupBroke`, with each enemy's choice to flee or give
  up); and settles the outcome (`EndEncounter`): reputation, grudge, memories, the chronicle, loot on the
  ground (`PickUpLoot`), robbery when downed, and what happens to those who surrendered.
- **Presentation decides everything real-time and reports it:** where bullets go, which zone they hit,
  enemy movement and AI, cover, and when someone has fled out of range.
- **No saving mid-fight.** `Simulation.WhyCannotSave()`, and `SimHost.Save` refuses. A state that has
  an encounter still round-trips through the save format, so a crash or quit mid-fight can be re-staged
  on load (presentation's job, step 3).
- **The event system doesn't start fights by itself.** `AmbushRequested` now carries `LeaderId` (the
  reaver the event cast) and `PlaceName`, and presentation sends `StartEncounter` when it can stage the
  fight. Until steps 2–4 land, ambushes are still only reported, so the current game doesn't get stuck in
  a fight nobody can see.

## Consequences

- Enemies are ordinary characters. A reaver you wound and who flees keeps the scar and remembers your
  name, which is what `reavers_remember` feeds on.
- Encounter content is YAML (`Content/encounters/`, schema in `docs/content-schema.md`), validated like
  everything else.
- The weapon blocks hold both rule values (sim) and feel values (presentation) so each gun is described
  in one place. Travis tunes both.

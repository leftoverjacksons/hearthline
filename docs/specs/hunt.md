# Spec: one hunt goal (M2)

Status: **draft, waiting for Travis's approval.** Nothing in it is built yet. It's the last missing piece of M2
before the gate (DESIGN: *M2: one hunt goal*).

## Goal

You set out to hunt one particular animal, the kind people talk about:

1. **Choose it:** at the job board, pick a hunt goal (to start, "Splitfoot", an old boar that has been
   tearing up the corn).
2. **Find it:** go out into the region and look for sign (tracks, rooted ground, a rub on a tree). Each sign
   points toward the next, and your Tracking skill decides how far along the trail one reading gets you.
3. **Approach it:** it hears and sees you. Crouching, wind and cover matter, and spooked it runs.
4. **Kill it:** a boar may charge instead of running. That's the Mark you might bring home: tusk wounds to the
   legs.
5. **Bring it home:** a heavy carcass or a unique pelt goes in the pack, and meat spoils on the way.

Legendary animals leave a unique pelt and a story: the chronicle and the gun's history remember it.

## Scope

**In:**
- one legendary animal, a boar (charges, so it uses the combat systems);
- a trail of sign spots;
- animal senses (sight, hearing with crouch, distance);
- flee and charge behaviour;
- hits through the existing hit zones and `ShotHit` (a Mark on the boar, the gun's ledger line);
- butchering into pelt and meat;
- hauling with the existing pack weight and spoilage;
- a hunt goal on the job board;
- cheats.

**Out:**
- ordinary game animals wandering the region, deer and wolf fights;
- horses and dragging carcasses (the salvage sled idea in `docs/IDEAS.md`);
- wind direction (later; for now hearing is by distance and stance only).

## Simulation

- **Content:** `Content/hunts/<id>.yaml`:
  - the animal (an archetype `boar` in vocab, with health, flee/charge chances and damage);
  - its unique items (`pelt_splitfoot`, `boar_meat` count);
  - sign count;
  - where it ranges (area tags);
  - outcome text for the chronicle.
- **State:** `SimState.Hunt` holds the hunt id, the animal's character id (the boar is a `Character`
  with `Archetype: boar`, so wounds, Marks and memory work like on people), sign found so far, and whether the
  animal knows about you.
- **Commands:**
  - `StartHunt` (job board);
  - `ReadSign` (a sign spot: Tracking stat + `SimRandom` decide how far along the trail you get);
  - `AnimalAlerted`;
  - `ShotHit` (existing; a boar is a character);
  - `ButcherAnimal` (gives the unique pelt and meat; needs a knife for full yield);
  - `EndHunt` (won, lost the trail, gave up).
- **Events out:** `HuntStarted`, `SignRead` (with the next sign's direction), `AnimalAlerted`,
  `AnimalCharged`, `HuntEnded`.

## Presentation

- **Sign spots** are interactables placed along the trail, inside the hunt's area tags. Reading one points the
  way with a short line ("Fresh rooting, heading toward the creek").
- **The boar** is a body like the reavers: hit zones, a nav agent, and its own small AI (graze, wander,
  alert, flee or charge).
  - A charge is a fast run at you; contact is a `ShotHit`-style tusk hit, a `Claw`-type wound on the legs.
  - It needs a greybox boar model (Blender), animated procedurally (body bob, leg swing) until there's a
    real one.
- **Senses:** sight cone and distance, hearing by distance × (crouched 0.4, walking 1, running 2). An alert
  meter shows when it has noticed you.

## Acceptance tests

- **Sim:**
  - reading sign advances the trail more with higher Tracking;
  - a hunt can't start twice;
  - the boar takes wounds like a person;
  - butchering gives the unique pelt once;
  - meat spoils on the way home (existing rule);
  - the chronicle and gun ledger record the kill.
- **PlayMode:**
  - the hunt goal places sign spots;
  - reading the last sign reveals the boar;
  - walking up upright alerts it from further away than crouching;
  - shooting it down lets you butcher it;
  - the pelt is in the pack.

## Decisions for Travis

1. **The first animal:** a boar (charges, uses the combat systems), or a deer (pure stealth and tracking)?
   My recommendation: the boar.
2. **Where hunts start:** the job board, or a rumour event? My recommendation: the job board for M2.
3. **Reference images** for the boar (`ArtReference/places/` or a new `animals/` folder), if you have a look
   in mind. Otherwise it's a greybox stand-in.

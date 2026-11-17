# Spec: one hunt goal (M2)

Status: **approved 2026-11-17** (Travis): a deer first; hunts start from the job board. **Built** (ADR 0023): `HuntTests`, `HuntPlayTests`. Fits the tone of the land
(DESIGN, *Exploration*): a lonely landscape where most of the danger is patience, distance and your own noise.

## Goal

You take up a hunt for one particular animal, the kind people talk about: **Crookhorn**, an old whitetail
buck with one antler grown crooked.

1. **Choose it:** at the job board, *Take up the hunt for Crookhorn*.
2. **Find him:** go out into the woods and look for sign (tracks, a rub on a sapling, droppings, a bed in the
   grass). Each sign points toward the next. Your Tracking skill decides how far one reading gets you; a good
   tracker skips ahead.
3. **Get close:** he sees and hears you. Standing, walking and running carry far; crouching carries less, and
   the long grass hides you. He raises his head first, then bolts. A bolted buck leaves the trail colder:
   you need more sign to find him again.
4. **Take the shot:** one clean shot, ideally to the chest or head. A wounded buck runs.
5. **Bring him home:** butcher him for venison (better with a knife), his hide and his antlers. They are heavy,
   and the venison spoils if the trip runs long.

He is a legend, so the hide is unique, the gun remembers him, and the chronicle records the day.

## Scope

**In:**
- one legendary deer (`Content/hunts/crookhorn.yaml`);
- a trail of sign spots laid through the woods;
- the deer's senses: sight cone, hearing by stance and speed, gunshots;
- graze, alert and flee behaviour;
- hits through hit zones and `ShotHit` (the deer is a character, so wounds work);
- butchering with a knife bonus;
- hauling with the existing pack weight and spoilage;
- job board entry;
- cheats.

**Out:**
- herds and ordinary deer;
- other animals (boar, wolves, bear);
- wind direction;
- blood trails;
- horses;
- the salvage sled.

## Simulation

- **Content:** `Content/hunts/<id>.yaml` holds:
  - the animal: archetype `deer` in vocab, and its name;
  - sign count and the area tags it ranges in;
  - yields: meat item and count, unique items, and how much a knife adds;
  - how many signs a bolt costs;
  - chronicle text;
  - the senses (sight range and angle, hearing per stance). These are presentation values, kept with the animal.
- **State:** `SimState.Hunt` holds the hunt id, the animal's character id, sign read so far (progress), whether
  he's in sight (revealed), start day and a layout seed. Sign positions come from the seed, so they're the
  same after a load.
- **Commands:**
  - `StartHunt` (refused while one is on);
  - `ReadSign` (the next sign: + 1, or + 2 with good Tracking and a seeded roll; reaching the count
    reveals him);
  - `AnimalBolted` (presentation saw him run off: progress drops, and he's hidden again);
  - `ShotHit` (existing);
  - `ButcherAnimal` (once he's down: yields into the pack or stores, the gun's ledger and the chronicle, the
    animal dies, the hunt is won);
  - `EndHunt` (give up).
- **Events out:** `HuntStarted`, `SignRead` (index, remaining, revealed), `AnimalBolted`, `HuntEnded`.

## Presentation

- **Job board:** a Hunts section.
- **Sign spots:** placed on the NavMesh in the hunt's area, spaced 25–40 m along a wandering line. Each is an
  interactable ("Read the sign: fresh tracks") with small ground marks. Reading one shows a line pointing
  toward the next.
- **The deer:** a greybox buck (Blender) moved procedurally (legs swing, head down to graze) until real
  animation exists. Hit zones: head, neck, chest (torso) and legs. When he's revealed, he grazes at the trail's
  end.
- **`DeerAI`:**
  - **graze:** wanders slowly with the head down;
  - **alert:** the head comes up and he stares; an alert meter fills from sight and sound;
  - **flee:** runs away on the NavMesh, reports `AnimalBolted` once far, then settles elsewhere;
  - **down:** lies down; butcher with E.

  A missed shot within 60 m alerts him at once.
- **HUD:** an unobtrusive meter while he's in sight: grazing, uneasy, watching you.

## Acceptance tests

- **Sim:**
  - starting creates the deer and refuses twice;
  - sign advances, and further with high Tracking;
  - reaching the count reveals him;
  - a bolt cools the trail;
  - butchering works once and needs him down;
  - the knife adds yield;
  - ledger and chronicle lines;
  - state round-trips the save;
  - content validation for hunts.
- **PlayMode:**
  - taking the hunt at the job board lays the trail;
  - reading every sign brings him out;
  - he notices someone standing further away than someone crouching;
  - a head shot drops him;
  - butchering puts the hide in the pack.

# 0023 — The hunt: a trail in the simulation, the animal in presentation

Status: accepted (spec approved 2026-11-17: a deer first, from the job board).

## Decisions

- **The hunted animal is a sim `Character`** (archetype `deer`), so wounds, Marks, being downed and death work
  on it the same way as on people. Content gives it its own health (Crookhorn: 2, so one clean chest, neck or
  head shot drops him). A leg hit is one step milder.
- **The sim holds the trail's state:** progress, revealed, bolts, the layout seed and the gun that made the
  kill. It decides sign reading (Tracking lets you skip ahead, with a seeded roll), the trail going colder
  after a bolt, and the butchering: yields with a knife bonus, the gun's ledger, the chronicle, and a
  once-only flag.
- **Presentation lays the trail from the seed** (`HuntDirector`), inside an area carrying the hunt's tags, so
  it's the same after a load. One sign shows at a time, with a line pointing to the next.
- **`DeerAI` decides when he notices you:**
  - sight is a cone, shortened by crouching and by his head being down;
  - hearing goes by your stance and speed;
  - a gunshot within earshot, or being hit, makes him bolt.

  Sense values live in the hunt's YAML; the rest are feel values. His legs and neck move procedurally until
  there's an animated deer.
- **Tone:** most of the danger on a hunt is your own noise and patience. Hostile people are rare
  (DESIGN, *Tone of the land*).

## Known rough

- Crookhorn is a greybox buck, and he falls by tipping over.
- There's no blood trail for a wounded deer: he bolts and the trail is laid again.
- Butchering takes everything at once and can overload the pack (intended for now: hauling home is the game).

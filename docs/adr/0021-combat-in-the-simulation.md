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

## Step 2 notes (player aim, fire, reload)

- **Aiming adjusts the Starter Assets follow camera** (distance, shoulder, field of view) instead of switching
  to a second camera. That leaves the mirror camera's blends alone.
- **Bullet spread is presentation randomness** (`UnityEngine.Random` in `PlayerWeapon`), not simulation state.
  Where a bullet goes is decided in real time. The sim only rolls jams, with `SimRandom`.
- **Guns are drawn on the body by `GunVisuals`** (Bridge), from the simulation's equipped gear: the long gun
  on the back, the sidearm on the hip, the drawn one in hand. Placement is in the character's frame, not bone
  axes. While aiming, the gun is placed from the shoulder along the aim line until real aim animations and
  IK exist.
- **A new game starts with a loaded lever rifle** (`NewGameOptions.StartingGear`, set on `SimHost`), plus 12
  cartridges in the stores.
- **The practice target** has real hit zones but no simulation character. `PracticeTarget` takes the hit and
  shows the severity it would have had, using the simulation's own rule (`Simulation.ShotSeverity`).

## Step 3 notes (sticky cover)

- **Cover is data the scene builder generates** (`CoverSurface` next to each solid collider that stands on the
  ground and is at least 0.6 m tall; under 1.35 m is low cover): a box footprint (faces to press against,
  corners to lean out at) or a ring (trunks, barrels: lean out on either side). There are about 1,780 in the
  region, 1,641 of them tree trunks. Rail fences and the clothesline are see-through, so they aren't cover.
  Open shapes like the fire pit's tripod count as high cover by their bounding box; good enough for now.
- **`PlayerCover` takes over movement while in cover.** It zeroes the controller's move input and sets the
  final position in `LateUpdate`, from its own anchor on the cover. The Starter Assets controller carries
  momentum from the body's velocity and otherwise pushes against the slide.
- **No cover animations yet.** Pressed in is the idle or crouch pose, sliding glides, and leaning out
  steps the body sideways past the edge. The clips are in `docs/NEEDS.md`.

## Step 4 notes (enemies)

- **Unity's AI Navigation package** (`com.unity.ai.navigation` 2.0.16, free; spec decision 5). The region
  builder bakes a NavMesh over the solid colliders (trigger volumes and hit zones ignored) into
  `Scenes/Generated/M2_Region_NavMesh.asset`.
- **`EnemySpawner` (Bridge) stages a fight** when the sim emits `EncounterStarted`, and re-stages one found in
  a loaded state. Bodies come from the generated `Enemy_Reaver` prefab: the mannequin with hit zones, a gun,
  a nav agent and the player's animator. Spawned bodies implement the Bridge's `ISpawnedCombatant`, so the
  Bridge never references Game types.
- **`EnemyAI` (Game) uses the same `CoverSurface` data as the player.** It picks the far side of cover within
  its gun's range, pops out to shoot, reloads and clears jams in cover, flanks one at a time when the player
  sits still in cover, lies down when downed, and flees or kneels when the group breaks. Accuracy starts poor
  and tightens while it keeps the player in sight. All shots are `FireWeapon` and `ShotHit`.
- **Bullets pass through movement capsules** (CharacterControllers) and land on the hit zones inside.

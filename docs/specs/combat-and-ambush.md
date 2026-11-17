# Spec: ranged combat and the ambush (M2)

Status: **approved 2026-11-17** with the default answers to the decisions below. Build step 1 (sim and content) is done: ADR 0021, `CombatTests`, `EncounterContentTests`. Step 2 (player aim, fire, reload against a practice target) is done: `WeaponTests`. Step 3 (sticky cover for the player) is done: `CoverTests`. Next: step 4, enemies.

## Goal

When an event says `ambush: reavers_road`, a real fight starts where you stand:
- a short quick-draw slowdown;
- 2 to 4 reavers who take cover, shoot, flank, and break and run (or give up) when it goes badly;
- you aim over the shoulder and fire a rifle or revolver with scarce ammunition.

Every hit, on you or on them, becomes a wound Mark through the systems that already exist. It is over in a minute or two, and it leaves a record: wounds, spent cartridges, a notch in the gun's history, a grudge, loot.

Design anchors (`docs/DESIGN.md`, *Combat and stealth*):
- deliberate pace;
- enemies drop in 1–3 hits, and so do you;
- sticky cover (changed from contextual on 2026-11-17, see *Cover* below);
- the quick-draw moment;
- downed, not dead;
- a simple tactical AI that takes cover, flanks, flees and surrenders.

## Scope

**In (M2):**
- the rifle and the revolver;
- aiming, firing, reloading, ammunition, jams;
- one encounter type fully staged (reavers, road and small variants);
- enemy AI: cover, shoot, flank, break;
- the quick-draw slowdown;
- fight outcomes: won, fled, they surrendered, you were downed;
- loot from the fallen;
- feeding Marks, gear history and grudges.

**Out (later):**
- shotguns, bows and oddities;
- weapon cleaning and maintenance (wear and jams are tracked now, cleaning comes later);
- cover-to-cover dashes, vaulting over cover, blind fire;
- stealth and takedowns;
- animals (`wolves_pack` stays reported only);
- companions fighting beside you;
- horseback.

## Where things live (architecture rules)

| Layer | Owns |
| --- | --- |
| **Sim** (pure C#, headless-tested) | Encounter definitions; the active encounter and its combatants (real sim `Character`s, so wounds and Marks work on enemies too); ammunition, jams and wear; hits (existing `InflictHit`); morale and breaking; outcomes, loot, reputation, grudges, memories, gear ledger lines. All randomness through `SimRandom`. |
| **Bridge** | Spawning enemy bodies for combatant ids; mapping hit zones to `InflictHit` (as `HitReceiver` already does); mapping sim events to visuals. |
| **Game** | Aim camera, crosshair, firing raycasts and spread, enemy movement and AI decisions, cover surfaces and the player's cover state, the slowdown, the HUD. Everything real-time and moment-to-moment. It sends commands and reacts to events. |

Real-time positions are not saved. **Saving is refused during a fight**, and quicksave is greyed out. If the game closes mid-fight, the sim still has the active encounter: on load it is re-staged around the player, and the enemies keep their wounds. That keeps rule 4 (state that must persist is in the sim) without saving bullets in flight.

## Data (content YAML)

### Weapons: a `weapon:` block on gear items

```yaml
# Content/items/lever_rifle.yaml (added)
weapon:
  ammo: cartridges          # item id consumed per shot
  capacity: 7               # rounds loaded at once
  damage: gunshot           # DamageType → existing wound marks
  severity: 2               # base wound severity on a body hit (head: +1, hands/feet: -1, min 1)
  range: 60                 # metres; beyond it, severity drops by 1
  fire_interval: 0.9        # seconds between shots (lever action)
  reload_per_round: 0.6     # seconds; scaled by the reload_speed stat
  spread: 1.2               # degrees at rest; scaled by aim_steadiness, movement and arm wounds
  jam_chance: 0.01          # per shot at full condition; rises as condition falls
  wear_per_shot: 0.002      # condition 1 → 0
```

Revolver: `ammo: revolver_rounds`, capacity 6, severity 2, range 25, fire interval 0.45, wider spread. The values are starting points; Travis tunes the feel numbers.

New items:
- `revolver_rounds` (ammo);
- a `reaver_rifle` and `reaver_revolver` pair (worn, low condition, what the reavers carry and drop).

### Encounters: `Content/encounters/<id>.yaml` (new content type)

```yaml
id: reavers_road
name: Reavers on the road
enemies:
  - { archetype: reaver, count: [2, 3], weapon: reaver_rifle, ammo: [6, 12], traits_any: [hot_tempered, cruel] }
  - { archetype: reaver, count: [0, 1], weapon: reaver_revolver, ammo: [4, 8], role: leader }
spawn:
  distance: [22, 38]        # metres from the player
  spread_degrees: 120       # an arc in front of the player
morale:
  break_when_down: 0.5      # the group breaks once half are down or fled…
  break_on_leader_down: true # …or the leader is down
  surrender_chance: 0.35    # when it breaks: surrender (else flee)
loot:                        # what the fallen leave, beyond their own gear
  - { item: cartridges, count: [2, 6], chance: 0.8 }
  - { item: old_coin, count: [1, 4], chance: 0.5 }
outcomes:
  won:       { reputation: { reavers: -5, homesteaders: 2 }, grudge: reavers }
  fled:      { reputation: { reavers: 2 } }
  surrender: { reputation: { reavers: -2, homesteaders: 3 } }  # then a choice: let them go, take their guns, or worse
  downed:    { robbed: true }    # they take your pack before leaving you to the existing bleed-out rules
```

The schema goes in `docs/content-schema.md`, with validation tests in `ContentValidation`:
- encounters, archetypes, items and traits must exist in the vocab or content;
- every `encounters:` id in `vocab.yaml` needs a file;
- count ranges must be sane.

`wolves_pack` gets a file with `staged: false`, so it is still only reported.

## Simulation

**State** (`SimState.ActiveEncounter`, null when no fight):
- the encounter id and the event that started it;
- combatant ids, with state `fighting`, `down`, `fled` or `surrendered`;
- each combatant's weapon (a real `GearItem`), loaded rounds and spare ammo;
- the start day.

`GearItem` gains `Condition` (0–1) and `Loaded` (rounds in the weapon).

**Commands:**

| Command | What it does |
| --- | --- |
| `StartEncounter { EncounterId, EventId }` | Generates the enemies (seeded; a reaver already cast by the event becomes the leader), gives them gear and ammo, and sets `ActiveEncounter`. Emits `EncounterStarted` with the combatant ids. Refused if one is already running. Also fired by the existing `AmbushRequested` path. |
| `FireWeapon { CharacterId }` | For the player or an enemy. Needs a round loaded. Emits `WeaponFired`, or `WeaponJammed` (rolled from jam chance and condition), or refuses with "empty". Wears the gun. |
| `ClearJam { CharacterId }` | Clears the jam (presentation plays the time it takes). |
| `ReloadWeapon { CharacterId, Rounds }` | Moves rounds from carried ammo (the player's pack or stores, or an enemy's supply) into the gun, up to capacity. |
| `InflictHit` (exists) | Now also works on combatants. After a hit, morale is checked. A combatant that goes down emits `CombatantDown`, and the gun that did it gets a ledger line ("Dropped a reaver at Miller's Ford, spring year 1"). |
| `CombatantFlees` / `CombatantSurrenders` | When the AI acts on a break, or presentation sees an enemy escape out of range. |
| `EndEncounter { Outcome }` | Won (all enemies down, fled or surrendered), Fled (the player got away), or Downed (the head of household is down with no companion). Applies the encounter's outcome block, drops loot and the fallen's gear on the ground (as gather-style pickups), writes memories for both sides (survivors remember the surname, which feeds `reavers_remember`), and clears `ActiveEncounter`. |
| `ResolveSurrender { Choice }` | Let them go, take their guns, or worse (a reputation and guilt hit). |

**Morale rule:** checked after any combatant changes state. When the share down or fled reaches `break_when_down`, or the leader is down and `break_on_leader_down` is set, the group breaks. Each remaining enemy rolls `surrender_chance`; the rest flee. This emits `GroupBroke`, carrying each enemy's choice, for presentation to act out.

**Lethality:** this uses the existing health and downed rules unchanged (health 3; a severity-2 body hit takes 2). An enemy drops in 1–2 rifle hits, and so does the player. A head hit adds +1 severity. Hands and feet take −1 but cost function: the wound marks already slow you or shake your aim.

**Events out:**
- `EncounterStarted`;
- `WeaponFired`, `WeaponJammed`;
- `CombatantDown`, `GroupBroke`;
- `EncounterEnded` (outcome, loot dropped, ledger lines).

## Presentation

**Player:**
- **Hold right mouse to aim:**
  - the camera moves over the shoulder and narrows its field of view (both are feel values);
  - the character faces where the camera looks and walks at aim speed;
  - a crosshair appears;
  - V swaps shoulder.
- **Left mouse fires:** a raycast from the camera through the crosshair, with spread, hits the Hitbox layer or the world. The sim decides whether the gun fired at all (empty or jam).
- **R reloads**, one round at a time for the lever rifle, interruptible.
- **1 / 2 draw** the rifle or the revolver; there's a draw time.
- Stand-in gun models: greybox rifle and revolver from Blender, in the hand while aiming and on the back or hip otherwise (the design's visible gear).
- Animations: rifle idle, walk and aim from Mixamo (have), firing and reloading (listed in `docs/NEEDS.md`). Until those arrive, recoil is a simple procedural kick.

**Cover (sticky):**
- **Q** snaps you to the nearest cover within about 1.5 m in the direction you're facing or moving, and Q
  again leaves it (so does moving firmly away). You turn your back to it and press against it: standing
  against high cover (walls, wrecks, trunks), crouched against low cover (logs, low walls, barriers, rocks).
- **Moving** left or right slides you along the cover. You stop at an edge rather than walking off it.
- **Aiming from cover:**
  - at an edge of high cover or a tree trunk, you lean out to that side;
  - behind low cover, you rise over the top;
  - letting go of aim takes you back into cover.

  The camera's shoulder follows the side you lean from.
- **While pressed in, you're hidden:** enemies lose line of sight unless they flank. Shots that hit the cover
  stop there. Leaning out exposes your head, shoulder and arm zones only, so a hit while peeking tends to be
  a head or arm wound. That is the Mark the fight leaves.
- **Cover surfaces are data, not guesswork.** The scene builder generates `CoverSurface` pieces along kit
  props from their shapes:
  - straight segments for walls, wrecks and logs, and rings for tree trunks;
  - each with a height class (low under 1.3 m, high above) and usable edges.

  The cheat panel can draw them.
- **The body sells it:** the pose presses against the surface, with the spine leaning out and the head and
  gun aimed by inverse kinematics. Unity's free *Animation Rigging* package does the aim; the cover idle,
  slide and lean poses come from Mixamo. Until those clips arrive, the stand-in is the crouch and rifle idle
  poses plus the IK lean.
- **Enemies use the same cover surfaces:** they press in, lean out to fire, and flank by moving to a surface
  at an angle to yours.

**Enemies:**
- tinted mannequins with the same hit zones as the player;
- moved by Unity's NavMesh (the free `com.unity.ai.navigation` package, baked by the scene builder).

The AI is a small state machine per enemy, with decisions in Game and outcomes through sim commands:
1. **Engage:** move to the nearest cover surface that faces the player, press in, and lean out to fire.
2. **Shoot:** fire in bursts with an accuracy that grows the longer they track you, so moving and breaking line of sight matter. Reload in cover.
3. **Flank:** one enemy at a time moves to cover at an angle to yours when you have been pinned for a while.
4. **Break:** on `GroupBroke`, either flee out of range (`CombatantFlees` when gone) or drop the gun and kneel (surrendered; walk up and press E to resolve).

**Quick-draw:** on `EncounterStarted`, time slows to about 35% for about 2 seconds while you draw. Both are feel values.

**HUD:** rounds loaded and spare, a jam prompt, a hit marker, and a red edge when you're hit. Bleeding wounds already show on the body.

**Ambush staging:**
1. `AmbushRequested` closes the event panel.
2. `StartEncounter` runs.
3. Enemy bodies spawn at sim-chosen ranges in the arc, snapped to the ground and nearby cover.
4. The road clock pauses (time waits for the fight, as it waits for events).

## Debug and cheats

The cheat panel gets a **Combat** tab:
- start any encounter here;
- give ammo;
- set gun condition;
- force a jam;
- kill, down or break the enemies;
- god mode (hits register as Marks but don't lower health);
- a toggle to draw AI states over enemies' heads.

## Acceptance tests

**Sim (EditMode, headless):**
1. `StartEncounter` creates the enemy count in the defined ranges, with their gear and ammo; the same seed gives the same fight; it's refused while one runs.
2. Firing uses a loaded round, refuses when empty, and reload moves ammo from the pack.
3. Jams: the rate at full condition is close to `jam_chance` over 10,000 shots, and rises at low condition. A jammed gun refuses to fire until `ClearJam`.
4. A rifle body hit on an enemy creates a gunshot wound Mark and takes 2 health. A head hit downs them. Hands and feet do −1.
5. Morale: downing half the group, or the leader, breaks it; surrender and flee split by the seeded roll.
6. `EndEncounter(Won)` drops loot and the fallen's gear, writes the grudge and memories, and adds a ledger line to the gun that dropped someone.
7. `EndEncounter(Downed)` empties the pack (robbed), and the existing bleed-out rules follow.
8. Save is refused during a fight. A state with an active encounter round-trips through persistence.

**Content validation:** encounter schema; vocab cross-references; weapon blocks reference real ammo items.

**PlayMode:**
1. An `ambush:` outcome spawns the enemies around the player, and the quick-draw slowdown happens.
2. A raycast "shot" at an enemy's head zone downs it in the sim.
3. An enemy shooting the player creates a wound Mark on the player.
4. Downing all enemies ends the fight with loot on the ground. Picking it up fills the pack.
5. Enemies move to cover surfaces and press in.
6. The player presses into cover with Q: the capsule sits against the surface and slides along it, stops at an edge, and leaning out to aim exposes only the upper body's hit zones. Low cover blocks an enemy's shot at a crouched player.

## Files expected

- **Sim:**
  - `Systems/CombatSystem.cs` (commands, morale, outcomes);
  - `Content/EncounterDefs.cs` and `Content/EncounterReader.cs`;
  - additions to `Model/SimState.cs` (ActiveEncounter, GearItem condition and loaded rounds), `Content/ItemDefs` (weapon block) and `Events/DomainEvents.cs`.
- **Content:**
  - `Content/encounters/reavers_road.yaml`, `reavers_small.yaml`, `wolves_pack.yaml`;
  - weapon blocks on `lever_rifle` and `revolver`;
  - new items `revolver_rounds`, `reaver_rifle`, `reaver_revolver`;
  - `docs/content-schema.md`.
- **Bridge:** `EnemySpawner.cs`, `CombatantView.cs`.
- **Game:**
  - `AimController.cs` (camera, crosshair, shoulder);
  - `PlayerWeapon.cs` (fire, reload, draw);
  - `PlayerCover.cs` (snap, slide, edges, lean or rise to aim);
  - `CoverSurface.cs` (segments and rings, height class, edges);
  - `EnemyAI.cs`, `QuickDraw.cs`, `CombatHud.cs`;
  - a Combat tab in `CheatPanel.cs`;
  - `RoadEvents.cs` (stage instead of report).
- **Editor:** the builder makes gun models, generates cover surfaces from kit props, bakes the NavMesh, and adds aim and fire states to the player animator.
- **Tests:** `Tests/EditMode/CombatTests.cs`, content validation additions, `Tests/PlayMode/AmbushTests.cs`.
- **Docs:** ADR 0021 (combat architecture: fight state in the sim, real-time in presentation, no saving mid-fight), SETUP controls.

## Build order

1. Sim and content, headless-tested (no Unity needed).
2. Player aim, fire and reload against a target dummy.
3. Sticky cover for the player: surfaces, snap, slide, lean and rise to aim, IK.
4. Enemies: spawn, NavMesh, cover, shooting.
5. Morale, outcomes, loot, the quick-draw slowdown, HUD and cheats.
6. Stage `reaver_toll` ("Draw") and `reavers_remember` end to end, then play it.

## Decisions for Travis

1. **Controls:**
   - right mouse aims, left fires;
   - R reloads, Q takes and leaves cover, V swaps shoulder;
   - 1 and 2 draw rifle and revolver;
   - C crouches.

   OK?
2. **Downed alone in an ambush:** the reavers rob your pack and leave you to the bleed-out rules (death or a permanent Loss mark). Or should they sometimes finish you?
3. **Ammo:** a separate `revolver_rounds` item, and the starting stores have 12 cartridges and 0 revolver rounds. Scarce enough?
4. **Surrender:** three choices (let them go, take their guns, or worse). Too much for M2, or keep it?
5. **Packages:** add Unity's free AI Navigation (enemy movement) and Animation Rigging (aim and lean IK) packages?

**Cost of sticky cover:** it is the largest piece of this spec, about a third of the work. Its look depends on
animations Mixamo has only some of. It is built as step 3 so the rest isn't blocked; until the clips arrive,
poses use IK on the crouch and rifle idle.

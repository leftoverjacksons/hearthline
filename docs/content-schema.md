# Content schema

What the YAML in `Content/` may contain. The loader (`Assets/Hearthline/Sim/Content`) is strict: an
unknown field, a bad reference or a broken rule is an error, and all errors are reported in one run.
Check with `tools/test.sh content`.

Covered: `vocab.yaml`, `names.yaml`, `marks/`, `traits/`, `factions/`, `items/`, `loot/`, `events/`.
YAML anywhere else is rejected.

## vocab.yaml

The closed lists other content refers to.

```yaml
stats:
  - { id: reload_speed, name: Reload speed, base: 1 }   # base is the value for an unmarked person
reaction_tags: [scarred, changed]                        # what NPCs and factions react to
abilities: [night_sight]
```

## marks/<id>.yaml

One mark per file. The file name must be the id.

```yaml
id: scar_burn                 # snake_case, unique
name: Burn scar
category: scar                # wound | scar | loss | exposure | knack | rite | age
regions: [face, hand_l]       # where it can sit: head face torso arm_l arm_r hand_l hand_r leg_l leg_r
heritable: none               # none | echo | latent
origin_text: "Burn scar from {cause}[ at {place}], {season} of year {year}"
initial_stage: scarred
stages:
  - id: scarred
    effects:
      - { reaction: scarred }
      - { stat: reload_speed, mul: 0.85, regions: [hand_l] }   # optional regions: only when the mark is there
      - { stat: persuasion, add: -1, regions: [face] }
      - { ability: night_sight }
    visual:                                  # asset keys for the bridge; all optional
      decal: scar_burn
      part_swap: eyepatch
      blendshapes: { crooked_bone: 1 }
      material_params: { iris_glow: 0.3 }
    after_days: 56                           # optional timer; must come with `then`
    then:
      - { to: other_stage, weight: 2 }       # weighted choice, rolled with the seeded RNG
      - { to: third_stage }
```

Rules:

- **No cosmetic-only marks.** Every stage needs at least one effect that applies on every region the
  mark allows.
- **Origin text.** Tokens are `{cause}` `{place}` `{season}` `{year}`. A `[bracketed part]` is dropped
  when a token inside it is empty. If `{cause}` appears outside brackets, adding the mark without a
  cause is refused. `cause` is a noun phrase: "the boar", "a Reaver".
- A stat's value is `(base + all add) × all mul`.
- Every stage must be reachable from `initial_stage`.

### Wounds

A wound never stays a wound. It needs exactly the stages `fresh`, `stabilized`, `infected` and
`healing`, each with a timer, plus `damage_types` and an `outcomes` table:

```yaml
category: wound
damage_types: [gunshot]       # gunshot | blade | claw | burn | blunt
initial_stage: fresh
stages:
  - id: fresh        # untreated
    after_days: 2
    then: [{ to: infected }]
  - id: stabilized   # after field treatment; buys time
    after_days: 8
    then: [{ to: infected }]
  - id: infected
    after_days: 6
    then: [{ resolve: loss, weight: 2 }, { resolve: bad_scar, weight: 1 }]
  - id: healing      # after infirmary treatment; must be exactly this transition
    after_days: 16
    then: [{ resolve: treated }]
outcomes:
  clean_scar: [{ def: scar_gunshot }]
  bad_scar: [{ def: scar_ragged }]
  loss:                                         # optional; first candidate whose regions match wins
    - { def: loss_fingers, regions: [hand_l, hand_r] }
    - { def: loss_limp, regions: [leg_l, leg_r], severity_delta: 0 }
```

- `clean_scar` and `bad_scar` each need one candidate without `regions`, so every wound can end.
- If `loss` is rolled on a region with no loss candidate, the wound becomes the bad scar.
- When a wound resolves, the mark keeps its id, origin and date. Its name is rebuilt from the new
  definition's `origin_text`.
- (stage effects and visuals are omitted above for brevity; they are required as for any stage.)

## Marks: inheritance and age

```yaml
heritable: echo                                   # or latent
inherited_origin_text: "Born pale-eyed, like {cause}, {season} of year {year}"   # {cause} is the parent's name
```

`inherited_origin_text` is required when `heritable` is not `none`, and not allowed otherwise.

```yaml
category: age
gained_at_age: 50          # everyone gains this mark on reaching the age; origin_text must not need {cause}
```

## traits/<id>.yaml

```yaml
id: steady_hands
name: Steady Hands
description: "Hands that do not shake, whatever else does."
inherit_chance: 0.4        # chance a child gets it from a parent who has it
innate: true               # can also appear in a newborn by mutation
effects:
  - { stat: aim_steadiness, mul: 1.1 }     # same effects as marks, without regions
```

## names.yaml

`given_names` (children and generated people) and `surnames` (generated people).

## vocab.yaml, continued

`area_tags`, `archetypes` and `encounters` are the lists events may use in `when`, `generate` and `ambush`.

## factions/<id>.yaml, items/<id>.yaml, loot/<id>.yaml

```yaml
id: reavers
name: Reavers
description: "Raiders working the roads and the river crossings."
```

```yaml
id: bandage
name: Bandage
weight: 0.1
tags: [medicine]           # free-form for now
```

```yaml
id: traveler_pack
rolls: 2                   # how many entries are drawn (with replacement)
entries:
  - { item: jerky, count_min: 1, count_max: 3, weight: 3 }
  - { item: bandage }      # count 1, weight 1
```

## events/<id>.yaml

```yaml
id: wounded_stranger_creek
tags: [encounter, roadside, moral]     # "danger" makes the storyteller hold back while the party is hurt
weight: 10                              # relative chance among events that fit (default 10)
cooldown_days: 30
intensity: 1                            # 1..3; bigger events are followed by a longer quiet
when:                                   # all optional; every condition given must hold
  area_tags_any: [creek, road]
  area_tags_all: []
  thin_level_min: 0
  thin_level_max: 1
  time_of_day: [dusk, night]            # dawn | day | dusk | night
  at_home: false
  flags_all: []
  flags_none: []
cast:
  hero: { from: party, role: head }     # required: every event casts the head
  friend: { from: household, role: any, traits_any: [haunted], marks_any: [scar_burn] }
  stranger: { generate: traveler, age_min: 20, age_max: 50, traits: [haunted] }
beats:                                  # the first beat is where the event starts
  - id: start                           # optional; needed only as a goto target
    text: "{stranger.name} is slumped against a cairn."     # tokens: {role.name} {role.surname} {role.full_name}
    choices:
      - label: Bind the wound
        requires:                       # optional; checked on the head unless `who` names a cast role
          who: hero
          item_any: [bandage, medkit]
          trait_any: []
          mark_any: []
          stat_min: { medicine: 1 }
          flags_all: []
        outcome:
          item: { bandage: -1 }                         # give (+) or take (-)
          loot_table: traveler_pack
          relationship: { stranger: 30 }                # that person's opinion of the head
          reputation: { homesteaders: -5 }
          mark: { target: hero, def: rite_guilt_shadow, chance: 0.2, region: head, severity: 1, cause: "robbing {stranger.full_name}" }
          hit: { target: hero, damage: claw, severity: 2, region: arm_l, cause: "the boar" }   # a wound, and lost health
          trait: { target: hero, add: haunted }         # or remove:
          flag_set: [saw_the_light]
          flag_clear: []
          recruit: stranger                             # a generated person joins the household
          ambush: reavers_small                         # asks presentation to stage a fight
          chronicle: "{hero.name} bound up {stranger.full_name}."    # a deed in the head's chronicle
          goto: start                                   # next beat; without it the event ends
          chance:                                       # one branch at most is rolled; p may total less than 1
            - p: 0.7
              recruit: stranger
            - p: 0.3
              ambush: reavers_small
```

Rules:

- An event must change something. `goto` and `chance` alone do not count.
- Every beat needs at least one choice without `requires`, so the player is never stuck.
- Every beat must be reachable from the first.
- A `mark` outcome needs `cause` when the mark's `origin_text` needs one. Use `hit`, not `mark`, for wounds.
- Without `region`, a mark lands on a random allowed region and a hit on a random region.
- The place in a mark's name comes from where the event happened, not from the event file.
- `chance` cannot be nested.

## settlements/<id>.yaml

```yaml
id: calvary_ford
name: Calvary Ford
home: true                       # exactly one settlement is home: the default for events that do not say where
faction: homesteaders
values: { prosperity: 35, safety: 40, strangeness: 10, openness: 30 }     # all four, each 0..100
baseline: { prosperity: 30, safety: 35, strangeness: 10, openness: 30 }   # where the town drifts when left alone
services:                        # open while every min and max holds
  - { id: doctor, name: Doctor, min: { prosperity: 45, safety: 30 } }
  - { id: telegraph, name: Telegraph, min: { prosperity: 75 }, max: { strangeness: 50 } }
residents:                       # one named, persistent person per role; a child takes over when they die
  - { role: shopkeeper, archetype: homesteader, age_min: 35, age_max: 55 }
```

## Events and the world

Additional `when` conditions:

```yaml
when:
  reputation_min: { homesteaders: 10 }
  reputation_max: { reavers: -20 }
  grudge_any: [reavers]                 # one of these factions holds a grudge against the family
  services_any: [saloon]                # open in the settlement the party is in (default: the home settlement)
  settlement_min: { safety: 30 }
  settlement_max: { openness: 45 }
```

Casting a townsperson: `shopkeeper: { from: settlement, role: shopkeeper }`. The event cannot start if
nobody holds that role.

Additional outcomes:

```yaml
outcome:
  settlement: { prosperity: 5, safety: -2 }      # pushes on the town; applied when the season turns
  grudge: { faction: reavers, reason: "{hero.full_name} robbed the ridge camp" }
  settle_grudge: reavers
  memory: { target: shopkeeper, text: "{hero.full_name} called me a cheat in my own store." }
```

## Items: value

`value: 4` is what the store pays for one, in `old_coin`. Buying costs double. Items without a value
cannot be traded. Items tagged `food` are what the household eats.

## structures/<id>.yaml

A fixed slot on the homestead. Tier 0 is "not built"; `BuildStructure` goes up one tier at a time.

```yaml
id: infirmary
name: Infirmary
starts_at: 0                       # tier a new homestead begins with
tiers:
  - { name: Cot, cost: { lumber: 3, bandage: 2 }, provides: { treatment: 1 } }
  - { name: Sickroom, cost: { lumber: 8, medkit: 1 }, provides: { treatment: 2 } }
```

`provides` is what the homestead has while the structure is at that tier (not added to lower tiers).
Keys: capacity, morale, treatment, defense, workshop, preserving, stabling, hygiene, warding.

## jobs/<id>.yaml

```yaml
id: farmer
name: Farmer
skill: husbandry                   # the stat that grows with the work and scales the yield
structure: garden                  # optional: must be built; higher tiers yield more
produces:                          # per season, before scaling
  - { item: cornmeal, count_min: 3, count_max: 5 }
provides: { morale: 1 }            # optional: homestead totals while someone holds the job
```

A job must produce or provide something.

## Events and the homestead

```yaml
when:
  home_min: { defense: 1 }         # homestead totals
  home_max: { defense: 0 }
  morale_min: 60
  morale_max: 40
outcome:
  morale: -10
  damage: garden                   # the structure loses a tier
```

## Items: gear

An item with `equip` is gear: each one is a unique object with its own history, carried by a person or
kept in the stores. Gear cannot be given as a count (`item:`, loot tables, job yields, build costs).

```yaml
id: lever_rifle
name: Lever rifle
weight: 3.5
tags: [gear]
equip:
  slot: long_gun            # base mid outer head hands feet accessory long_gun sidearm knife
  visual: rifle_lever       # asset key for the bridge
  effects:                  # while worn or wielded; same effects as marks, without regions
    - { stat: marksmanship, add: 1 }
  heirloom_effects:         # in addition, for a blood descendant of whoever first carried it
    - { stat: aim_steadiness, mul: 1.08 }
```

Event outcome: `gear: { def: lever_rifle, to: hero, note: "Found in a wrecked truck by {hero.full_name}." }`

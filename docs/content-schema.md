# Content schema

What the YAML in `Content/` may contain. The loader (`Assets/Hearthline/Sim/Content`) is strict: an
unknown field, a bad reference or a broken rule is an error, and all errors are reported in one run.
Check with `tools/test.sh content`.

Only `vocab.yaml` and `marks/` have a schema so far. YAML placed in `events/`, `items/`, `traits/` or
`factions/` is rejected until its schema exists.

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

# 0004 — Health, lineage, inheritance and succession (first pass)

Status: accepted as defaults, built without spec review (2026-11-16). Every number is a first guess in
`LifeRules`.

## Decisions

- **Health.** Three points. A hit costs its severity. At zero the character is **downed**, not dead.
  `ReviveCharacter` brings them back at 1. Left alone (`LeaveDowned`, or a day passing) there is a 50%
  chance of death; otherwise their worst wound becomes a permanent loss (or a bad scar where nothing can
  be lost). Health returns one point a day while no wound is fresh or infected.
- **Household bonds.** Founder, Blood, Partner, Ward, Sworn. Heirs are living adults in the order
  Blood, Ward, Sworn, as the design lists. Partners and retired heads cannot inherit.
- **Death of the head** leaves the household headless until `ChooseHeir`. With no eligible heir the line
  ends (`LineEnded`), which the design gives as the default.
- **Retirement** is allowed from Elder age and needs a named heir.
- **Birth.** Any two living adult household members aged 18–45 can have a child; no sex or gender is
  modelled. Interludes (`PassSeason`) roll 15% per partnered couple per season. Children take the
  household surname.
- **Inheritance.** Each parental trait passes by its own `inherit_chance`; 15% of newborns gain one
  `innate` trait. Marks with `heritable: echo` appear in a child 25% of the time. Marks with
  `heritable: latent` appear 35% of the time; otherwise half the children carry them unseen
  (`CarriedLatent`) and can pass them on, which is how the Knack skips a generation. Inherited marks
  start at the definition's first stage, severity 1, named by `inherited_origin_text`.
- **Aging.** Age marks with `gained_at_age` arrive on their own. From 66, each season at home carries a
  rising chance of natural death (3% + 1% per year).
- **Chronicle.** The sim appends an entry for every mark gained or changed, birth, joining, partnership,
  death, retirement and succession. Entries are plain text, stored in the save.

## Open (the design doc lists the first three as open questions)

- Whether partners should be able to inherit, and whether the line can be rescued by adoption.
- How births are framed, and whether who can have children with whom should be constrained.
- Elders as mentors (training and morale bonuses) are not modelled; `Retired` is only a flag.
- Bequeathing gear waits for items with history.

# 0017 — A working homestead

Status: accepted (2026-11-16). Second step of the "functional, explorable homestead" stretch.

## Decisions

- **The yard shows the simulation's truth.** Each of the three slice buildings (dwelling, hearth,
  infirmary) is a `StructureView` holding one model per tier, index 0 being a build site. Before this the
  yard showed a cabin while the simulation said tent. Tiers come from content (`Content/structures`);
  models are from the Blender kit: tent, cabin, farmhouse; fire pit, kitchen, smokehouse; cot, sickroom,
  surgery.
- **One panel per building, opened with E** (`HomeBuilding`, the same Interactable pattern as gathering):
  what the current tier gives, its use, and the next tier's cost with a Build button that sends
  `BuildStructure` (refused with what is missing). Uses: the dwelling's **Sleep until morning** (the
  clock's camp, then a storyteller turn for home events); the hearth's household overview (morale, food,
  preserving, who does what, the last season report); the infirmary's wound list with **Treat**
  (`TreatWoundAtHome`, so the built tier and the best healer at home decide the odds).
- **Job board** (`JobBoard`): a row of jobs per household member, sending `AssignJob`.
- **Household figures** (`HouseholdFigures`): everyone at home but the head stands at their job's spot
  (or an idle spot), tinted per person, with a name and job label up close. Rebuilt on household, job and
  trip changes. Stand-in mannequins; they do not walk or work.
- New games start with 12 lumber, enough for the cabin or the cot.

## Not done

- The other six structures (workshop, stable, garden, washhouse, tannery, shrine) have no slots yet.
- Building is instant; there is no construction time.
- The last season report shown at the hearth is kept by presentation and is lost on load.

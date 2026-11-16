# 0003 — Mark model: instances point at definitions; wounds transform in place

Status: accepted (2026-11-16)

## Context

The design doc sketches a `Mark` that carries its own `effects[]` and `visual`. It also says wounds
become scars, infections or losses, and that a mark is named by the event that caused it.

## Decision

- A mark instance in the save stores identity and history only: definition id, stage, region, severity,
  origin (event id, cause, place), date gained, origin text. Effects and visuals are looked up from the
  definition and current stage. Balance and art changes therefore apply to existing saves.
- A save that refers to a definition or stage content no longer has keeps the mark (name and origin
  text intact) but it has no effects; `Simulation.FindOrphanedMarks` reports it.
- Stage timers are data (`after_days` / `then`). The wound stages (`fresh`, `stabilized`, `infected`,
  `healing`) are fixed names because treatment commands drive them.
- When a wound resolves, the same mark instance changes definition and category. It keeps its id, origin
  and original date, and its name is rebuilt from the new definition.
- Infection is a wound stage, not a separate mark.
- Effects can be limited to regions, so one "burn scar" slows reloads on a hand and costs persuasion on
  a face.
- Infirmary odds are in `TreatmentRules` (code, first-guess numbers), not yet in content.

## Not decided

- Severity does not scale effects yet; it feeds treatment odds and is available to visuals.
- Health, the downed state, and death from wounds (design: "Hits become Marks", step 4) are not modelled.
- Inheritance (`heritable`) is stored and validated but nothing uses it until M4.
- `Simulation.State` is exposed as mutable objects. Rule 2 (presentation never mutates state) is held
  by convention and review, not by the type system.

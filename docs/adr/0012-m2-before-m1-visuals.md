# 0012 — Play the fast loop (M2) before M1's visual art

Status: accepted (2026-11-16, Travis).

## Context

M1 ("Body as record") is done except for the visual layer: mark data, wounds from hit zones, treatment,
the 23 mark definitions and the greybox mirror are built and tested. What remains is decals, exposure
materials, part swaps and blend shapes, and the gate ("6 or more marks read as a person with a history in
one screenshot") is an art judgement.

No character model or art direction is chosen, and no assets are being bought for now (ADR 0010). How
decals, skin materials and part swaps are built depends on the real character's mesh, UVs and shaders,
so placeholder work on the Starter Assets mannequin would mostly be thrown away. Meanwhile the simulation
is far ahead of the playable game, and the riskiest open question is whether the loop is fun.

## Decision

- Split M1. Its code is finished in greybox, plus a **visual-key table**: every `visual` key that content
  uses maps to an asset slot, and a content test fails if a key has no entry. That gives the art work an
  exact deliverables list. No decals, shaders or part-swap meshes are made yet.
- Go to **M2 (fast loop greybox)** next. Its gate, "one out-and-back trip is fun in greybox", needs no art.
- Travis works on art direction and reference art in parallel (`ArtReference/`). M1's visual layer is built,
  and its gate judged, once a character and style are chosen.

## Consequences

- M1's gate stays open while M2 is built. The design's "stop at a failed gate" rule is not broken: the gate
  has not been tried, because it cannot be judged without art.
- Hit zones, mark anchors and the mirror were built against the mannequin's humanoid bones, so they carry
  over to a real humanoid character.

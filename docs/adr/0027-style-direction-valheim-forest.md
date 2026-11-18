# 0027 — Style direction: Valheim-like, made by us, and a forest

Status: accepted (Travis, 2026-11-18; spec docs/specs/look-pass.md). Supersedes the realism direction of
ADR 0022.

## Context

Travis named Valheim, The Long Dark and Medieval Dynasty as the look, then picked **Valheim**. The built
world was grey primitive boxes, and the one style test (ADR 0022) had gone toward photo-scanned realism:
photo rocks and grass, and a realistic homesteader. Two styles at once is what DESIGN warns makes solo games
look cheap.

## Decisions

- **One style, Valheim-like:** chunky low-poly shapes, crunchy and simple textures, and dramatic light and
  fog carrying the mood. It fits DESIGN's *painterly realism*, and it hides the weak spots of an AI-built
  game (animation, faces) better than realism would.
- **The photo-scanned direction is dropped.** The Rocks FREE pack is no longer used by built scenes, and the
  style test scenes are kept only as a record. The homesteader moves to the painted materials (spec step 7).
- **We make our own models, above all for the environment** (Blender, through the Blender connection), so
  everything shares one hand and can be committed to the public repo. Asset Store packs only where making
  them is unreasonable, and git-ignored as before.
- **The land is forest.** The region is North Georgia and southern Appalachia, so you're under a closed
  canopy most of the time. Open ground is made by people, water and the odd bald. The generator,
  tree models, understory and performance work all follow from this (spec step 3).
- **The look is judged from fixed screenshots** (`StyleFrames`): the same eight shots after every step, so
  Travis can judge away from home.

## Consequences

- DESIGN's *Asset strategy* ("buy, don't build, for environments") no longer holds for the environment. It
  is updated to point here.
- Closed forest shortens sight lines. Hunting distances, enemy AI ranges and the deer's senses will want
  retuning (Travis tunes).
- NEEDS' "leaf-card oak vs blob trees" question is settled: both give way to the modelled trees.

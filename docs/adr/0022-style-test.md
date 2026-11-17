# 0022 — Style test: grounded realism, world first

Status: proposed (first pass, 2026-11-17). Travis judges it in the Editor.

## Context

Travis wants realism for immersion: "the feeling of wandering around in a living natural world". The design
already says *painterly realism*, and warns that mixed asset styles make solo games look cheap. We stay on URP
(Travis's call, after weighing HDRP). Before matching the region and characters to a target look, one corner
of the homestead was built toward that look, to see it rather than argue about it.

## Decisions

- **Two generated scenes, one corner of the homestead** (`StyleTestSceneBuilder`): `StyleTest_GoldenHour` and
  `StyleTest_FoggyMorning`. Same layout, different light, fog and grade.
- **Photo-sourced ground and rocks** from the free Rocks FREE pack (DEXSOFT), git-ignored in
  `Assets/ThirdParty/RockFREE`. They're wrapped in our own URP materials as scene overrides, so the pack stays
  untouched. The pack's rocks are cliff-sized at scale 1, so placement sizes them in metres.
- **`Hearthline/GroundBlend`:** grass and dirt texture sets blended by vertex colour, with the edge broken by
  the grass's own detail.
- **Photo grass cards** (three crossed cut-out quads) instead of greybox blades. Leaf-card oaks instead of
  low-poly pines.
- **The homesteader** (MPFB, CC0 MakeHuman assets): built in Blender
  (`ArtSource/Characters/test_homesteader.blend`, with an `_editable` copy that keeps the body sliders).
  - His textures are baked with our colour treatment.
  - The trousers are split from the shared jeans texture by their UV islands.
  - He uses MPFB's `mixamo_unity` rig, re-rested to a T-pose so Unity's humanoid retargeting works.
  - Hem weights are cleaned: no arm or hand influence near the hips.

  `CharacterKit` imports him as a humanoid with an explicit bone map (Unity's automatic mapping misses this
  rig's feet) and builds URP materials.
- **Priority** (Travis agreed): sound, motion and nature before character fidelity. In a third-person wanderer
  the world fills most of the frame.

## Known issues

- The cabin and props are still greybox and now look like the weakest part of the frame.
- The foreground grass reads dark against the sun.
- The homesteader uses a stand-in idle, has a small jacket flap at one thigh, and no beard or hat.
- The sky's horizon turns pinkish-purple in fog.

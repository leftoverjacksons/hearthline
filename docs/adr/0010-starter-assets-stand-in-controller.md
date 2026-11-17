# 0010 — Unity Starter Assets as the stand-in third-person controller

Status: accepted (2026-11-16). Travis is not buying assets for now.

## Context

The design (M0, and the "Animation and feel" risk) calls for a *purchased* third-person controller and
animation base. No assets are being bought for a while, and the goal is to get as far as possible without
them.

## Decisions

- **Unity's free "Starter Assets – Third Person"** (Unity Technologies, Asset Store) is the controller,
  follow camera and humanoid mannequin until a purchase. It lives untouched in
  `Assets/ThirdParty/StarterAssets` (moved there from the import location; GUIDs are unchanged).
- **The pack is not in git.** The repository is public and Asset Store content may not be redistributed,
  so the folder is git-ignored and each machine imports it (`docs/SETUP.md`). Without it,
  `Hearthline.Game` does not compile in Unity and the greybox scene has missing prefabs; the headless sim
  tests (CI) do not depend on it.
- **Cinemachine 3** (`com.unity.cinemachine`) came in as its dependency. That matches the design's camera
  choice.
- **Hearthline wraps it, never edits it.** The greybox scene uses the `PlayerArmature`, `MainCamera` and
  `PlayerFollowCamera` prefabs as they are; our additions (mark anchors, `CharacterMarksView`,
  `PlayerInputRelease`) sit on the scene instance. `Hearthline.Game` references `Unity.StarterAssets`
  only for `PlayerInputRelease`.
- **Mark anchors ride on humanoid bones.** The scene builder puts one anchor per body region under the
  Animator's humanoid bones, so marks follow the animated body. Any humanoid character that replaces the
  mannequin gets anchors the same way.
- **Feel values stay at Starter Assets' defaults.** They are serialized on the controller and camera, for
  Travis to tune.
- **Greybox ground is a generated mesh, not a Unity Terrain.** The project does not include the terrain
  modules, and without them Terrain and TerrainCollider are stripped in Play Mode. Adding
  `com.unity.modules.terrain` and `com.unity.modules.terrainphysics` is the step to take when real terrain
  is wanted.

## Consequences

- Replacing the controller later means swapping prefabs in `Assets/Hearthline/Editor/SceneBuildKit.cs` and
  rewriting `PlayerInputRelease`; sim, bridge and tests are unaffected.
- The mannequin has no skin-detail layers, so the M1 visual layer (decals, materials, part swaps) still
  needs a real character model. Marks are drawn as greybox markers until then.
- Its editor script raises four obsolete-API warnings (`FindObjectsSortMode`), and a mobile UI file warns
  about the moved stylesheet path. Both are in third-party code we do not modify and do not use.

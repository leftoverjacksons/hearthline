# 0031 — Characters: the player stays the robot; character models to be made elsewhere

Status: accepted (Travis, 2026-11-18, after seeing the first pass). Look-pass step 7 is paused.

## What was tried

Travis chose realistic proportions with painted textures, after `ArtReference/people` (three homesteader
turnarounds). A first pass on the MakeHuman homesteader:
- **`SceneBuildKit.UseHomesteader`:** puts any humanoid model in place of the Starter Assets robot under the
  player's Animator, with the robot's Geometry and Skeleton removed and the avatar swapped. Animations, hit
  zones and Mark anchors carry over (they're found by humanoid bone). All 81 PlayMode tests passed with him
  as the player.
- **`ArtSource/Characters/fix_and_export_homesteader.py`:** two jacket hem vertices weighted to the forearms
  rose with the arms when the rig was re-rested from A-pose to T-pose, and made spikes at the hips. They're
  pulled back into the surface with their neighbours' weights, and the model is re-exported with the settings
  it has always used. Kept.
- **`ArtSource/Characters/paint_homesteader.py`:** Cycles bakes each part's surface position, and the
  textures are repainted by region after the reference: jacket, yoke, cuffs, shirt, trousers, knee patches,
  boots, greying hair and a beard. Originals are kept in `ArtSource/Characters/source_textures`.

## Decision

- **The result wasn't good enough** (Travis). MakeHuman's stock clothes and hair are the wrong shapes, so
  painting can't make them the reference.
- **The player goes back to the robot:** `SceneBuildKit.PlayerWearsHomesteader = false`. The homesteader's
  original textures are restored, and the paint script is kept only as a record.
- **Character models are to be made outside Unity and Blender scripting.** The leading option is image-to-3D
  from the turnarounds (Tripo, Meshy, Rodin), rigged by the tool or Mixamo's auto-rigger, then cleaned up
  in Blender and brought in through `UseHomesteader`. Character Creator 4 is the fallback if generated models
  fall short. To be discussed.
- No free asset in the project, or any found in the Asset Store, beats the robot for a realistic
  Appalachian homesteader. Mixamo's character library is the nearest free stand-in, and needs Travis's
  Adobe login.

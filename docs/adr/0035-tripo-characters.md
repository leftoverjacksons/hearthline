# 0035 — Characters from Tripo, and the first one as the player

Status: accepted (Travis, 2026-11-18: "please try and import and make that the main char if it works").
Follows ADR 0031, which chose image-to-3D for characters.

## Context

Travis made a woman in Tripo (`ArtSource/Characters/femaleTest1/`). The export has:
- a Mixamo-named rig, in T-pose;
- one mesh of about 19,000 triangles and one material, with 4K base colour and 2K normal, metallic and roughness maps.

Tripo exports come out about one metre tall, with a 0.01-scaled armature.

## Decision

**Blender export:** `ArtSource/Characters/export_tripo_character.py` brings a Tripo export into the project.
- It scales the character so the top of the head is at 1.70 m (an argument), and applies the scale.
- It exports mesh and rig, with no embedded media, to `Assets/Hearthline/Art/Characters/<Name>/<Name>.fbx`.
- It copies the base colour and normal maps beside the FBX.
- It packs metallic and roughness into one URP mask: R is metallic, A is smoothness (1 − roughness).

**Unity import:** `CharacterKit.SetUpTripo(name)` imports the character as Humanoid.
- It uses the same explicit Mixamo bone map as the old homesteader.
- It builds one URP Lit material from the textures.

**The player:** `SceneBuildKit.PlayerCharacter` names the character the player wears. It's `FemaleTest1` for now.
- Her meshes and rig replace the robot's under the same Animator.
- Animations, hit zones and Mark anchors carry over, because they're found by humanoid bone.
- If her files aren't there, the player stays the robot.

**Licence:** her files are git-ignored until Travis confirms that his Tripo plan allows a public repo (docs/NEEDS.md).

## Consequences

- **Fresh checkouts:** a fresh checkout without her files builds the robot, as it does without the Mixamo clips.
- **The backpack:** it's part of her mesh, so it can't come off. Guns carried on the back may pass through it. Separate pieces (modular clothes and gear) are Travis's next step.
- **Tuning:** her height is set by the export script's argument. The CharacterController is still sized for the robot (1.8 m), which suits her well enough.

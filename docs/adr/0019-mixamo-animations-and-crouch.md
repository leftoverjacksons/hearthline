# 0019 — Mixamo animations and crouch

Status: accepted for the stand-in controller (Travis tunes the feel values)

## Context

M2 needs movement and cover (crouch, then aim, cover and peeking), and the user chose to wait for real
animations rather than fake poses. They downloaded free Mixamo clips. The repo is public, Adobe's terms don't
allow redistributing the raw files, and CLAUDE.md says third-party assets are never modified.

## Decisions

1. **Clips live in `Assets/ThirdParty/Mixamo/`, git-ignored.** `docs/NEEDS.md` lists which clips to download
   and how (FBX for Unity, without skin, 30 fps).
2. **An import rule, not hand settings.** `Editor/MixamoImport.cs` (an `AssetPostprocessor`) sets the files to
   Humanoid, bakes root motion into the pose (the controller moves the character), and loops idles, walks
   and aims. These are Unity's import settings for the file, not edits to it. The settings live in the
   git-ignored `.meta` files, so a rule is the only way a fresh checkout gets them.
3. **One shared T-pose avatar.** Skeleton-only Mixamo files rest in their first frame, so an avatar built
   from "Crouching Idle" is mapped from a crouch, and the retargeted pose comes out bent (head thrown back,
   hand on the floor). Every clip copies the avatar of Mixamo's `T-Pose.fbx` once it is in the folder. The
   rule reimports the others when it arrives.
4. **The player animator is generated.** `Editor/PlayerAnimatorBuilder.cs` copies the Starter Assets
   controller to `Assets/Hearthline/Art/Animation/Player.controller` and adds the `Crouched` bool and a Crouch
   blend tree (idle ↔ crouched walk on `Speed`). Without the Mixamo clips, the plain Starter Assets controller
   is used and crouch still works, just without its pose. Further stances (aim, cover) go in the same
   builder.
5. **Crouch is presentation only.** `Game/Crouch.cs`:
   - C toggles crouch (B / east on a pad). Jump, or sprint while moving, stands up.
   - Standing waits for headroom.
   - The capsule shrinks from the top, so the player fits behind low cover, and the camera target drops.
   - Crouching is a moment-to-moment stance, not saved state. Stealth (awareness, noise) will read
     `Crouch.IsCrouched` when it lands; if a sim system ever needs it, it arrives as a command.
6. **Speed factors stack.** `Game/PlayerSpeed.cs` owns the controller's walk and sprint speeds and multiplies
   every `IMoveSpeedFactor` on the player (pack weight and crouch now; wounds and limps later), so they no
   longer overwrite each other. The execution order is PackLoad, then Crouch, then PlayerSpeed, then the
   controller.

## Not done yet

- The stand-to-crouch and crouch-to-stand clips are long (3–4 s). For now the stances crossfade (0.2 s).
- Crouch strafing and walking backwards use the forward crouch walk until the directional clips arrive.
- Cover lean and peek, aim stance, shoulder swap: next, once the clips in `docs/NEEDS.md` are in.

## Feel values (for Travis)

These are in the Inspector on the player:

- Crouch: speed 1.4 m/s, height 1.15 m, camera drop 0.5 m, transition 0.18 s, sprint stands up.
- `PlayerAnimatorBuilder`: crouch walk blend speed (1.4) and stance blend time (0.2 s).

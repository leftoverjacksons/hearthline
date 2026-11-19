# Known problems

Things that are wrong or look wrong now, as opposed to new ideas (`IDEAS.md`) or things only Travis can
provide (`NEEDS.md`). Newest at the top of each section. When one is picked up, note who and when. When it's
fixed, tick it and give the commit.

## Characters

- [ ] **The player's face looks off: shifted, and not as good as in Tripo.** (Travis, 2026-11-19.) She came in through `ArtSource/Characters/export_tripo_character.py` and `CharacterKit.SetUpTripo` (ADR 0035). Things to rule out, cheapest first:
  - **Lighting:** a render in Unity next to Tripo's viewer. Tripo shows a studio light. The region has fog and colour grading.
  - **Normal map convention:** if Tripo writes a DirectX (Y-down) normal map, Unity, which expects OpenGL, lights the face inside out. The fix is to flip the green channel.
  - **Texture compression:** the 4K base colour goes through Unity's default compression, which can smear the eyes and lips. Try high quality or no compression.
  - **The smoothness mask:** built as 1 − roughness, so the skin may read as plastic.
  - **Skinning:** Tripo's auto-rig weights around the face. Mixamo clips retargeted through the Humanoid avatar can pull the jaw and neck. Compare the T-pose with no animator against the idle.
  - **The export itself:** the scale and apply step, and Blender's FBX smoothing. Diff the vertex positions against the source.
- [ ] **The rifle doesn't sit right in her hands.** (Travis, 2026-11-19.) `GunVisuals` (Bridge) places the gun procedurally:
  - at the right hand plus `handOffset`;
  - when shouldered, from the right upper arm plus `shoulderedOffset`.

  The offsets are serialized, but they were set by eye on the robot. Nothing puts the hands on the gun: there's no IK. So the gun and the clip's hands can disagree. Likely fixes:
  - left-hand (and right-hand) IK to grip points on the gun;
  - offsets per character.
- [ ] **The slung rifle pokes through her backpack, and the pack blocks the aim camera.** (2026-11-18.) Wait for modular pieces (NEEDS: Modular character pieces), or move the sling point and the aim camera's shoulder offset for her.
- [ ] **Animations are still off in places.** (Travis, 2026-11-18.) Rifle, cover and crouch need a hands-on pass together. In cover with your back to the wall, the camera looks at her face. The other choice is sideways to the wall, over the shoulder.

## World

- [ ] **The trees look disjointed and cartoonish.** (Travis, 2026-11-19.) These are the greybox kit trees (separate trunk and canopy pieces, flat colours) scattered by `RegionSceneBuilder`. Ultimate Nature is imported but unused (git-ignored, in `Assets/ThirdParty/UltimateNature`). Swapping to its trees, or to a better free pack, is probably the fix. Needs a look pass of its own with style frames.

## Interface

- [ ] **Actions need a menu instead of a key each.** (Travis, 2026-11-18.) Make camp is on B for now because C is crouch. Idea: hold Tab for a radial, or tap it for a list (camp, rest or wait, journal). Needs a spec. Tabled on 2026-11-19.

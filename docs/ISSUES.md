# Known problems

Things that are wrong or look wrong now, as opposed to new ideas (`IDEAS.md`) or things only Travis can
provide (`NEEDS.md`). Newest at the top of each section. When one is picked up, note who and when. When it's
fixed, tick it and give the commit.

## Characters

- [ ] **The player's face looks off: shifted, and not as good as in Tripo.** (Travis, 2026-11-19.) Partly fixed on 2026-11-19, so check it in game.
  - **Not the export:** in a side-by-side render under the same light, my Blender export and the Tripo bridge import look the same in T-pose. The textures and material match, and mine is a bit sharper (4K).
  - **The cause:** the animation sheared her face. Tripo's auto-rig tied the mouth and chin about 30% to the neck bone, so any head turn or tilt in an animation pulled the lower face lopsided.
  - **The fix:** `export_tripo_character.py` now makes the face move entirely with the head, blending only across the throat.
  - **Second pass (still distorted, Travis, 2026-11-19):** the first pass skipped any vertex with a trace of shoulder weight, which covered her cheeks and temples. A lock of hair at eye height was also tied to her left arm. Now every vertex above the throat and within reach of the head follows only the head and neck (1,038 vertices).
  - **Left: the mesh itself.** Tripo's rigged export has about 19,000 triangles and only about 200 vertices on the front of the face. The face detail is mostly painted into the texture, and hair cards lie on her cheeks. If her face in Tripo's viewer looks better, that viewer is probably showing the model before it was cut down for rigging. Try exporting with a higher face count.
- [ ] **The rifle doesn't sit right in her hands.** (Travis, 2026-11-19.) `GunVisuals` (Bridge) places the gun procedurally:
  - at the right hand plus `handOffset`;
  - when shouldered, from the right upper arm plus `shoulderedOffset`.

  The offsets are serialized, but they were set by eye on the robot. Nothing puts the hands on the gun: there's no IK. So the gun and the clip's hands can disagree. Likely fixes:
  - left-hand (and right-hand) IK to grip points on the gun;
  - offsets per character.
- [ ] **She carries two rifles, and the pack blocks the aim camera.** (2026-11-18, cause found 2026-11-19.) The barrel that sticks up behind her shoulder is part of Tripo's mesh. Our game's slung rifle sits beside it. Either regenerate her without the rifle in Tripo, or cut it out of the mesh in Blender. Wait for modular pieces (NEEDS: Modular character pieces), or move the sling point and the aim camera's shoulder offset for her.
- [ ] **Animations are still off in places.** (Travis, 2026-11-18.) Rifle, cover and crouch need a hands-on pass together. In cover with your back to the wall, the camera looks at her face. The other choice is sideways to the wall, over the shoulder.

## World

- [ ] **The trees look disjointed and cartoonish.** (Travis, 2026-11-19.) These are the greybox kit trees (separate trunk and canopy pieces, flat colours) scattered by `RegionSceneBuilder`. Ultimate Nature is imported but unused (git-ignored, in `Assets/ThirdParty/UltimateNature`). Swapping to its trees, or to a better free pack, is probably the fix. Needs a look pass of its own with style frames.

## Interface

- [ ] **Actions need a menu instead of a key each.** (Travis, 2026-11-18.) Make camp is on B for now because C is crouch. Idea: hold Tab for a radial, or tap it for a list (camp, rest or wait, journal). Needs a spec. Tabled on 2026-11-19.

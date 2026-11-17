# 0011 — Hit zones: physical hits become wounds on a body region

Status: accepted (2026-11-16). First pass of M1's "wounds from hit regions".

## Decisions

- **Zones on bones.** Each character has trigger colliders (`HitRegion`) parented to its humanoid bones,
  one or more per body region: a sphere for the head, a thin box in front of it for the face, capsules
  for torso, arms, hands and legs (feet count as legs). A zone follows its bone as the body animates.
  The scene builder places them from the Animator, so any humanoid character gets them the same way.
- **Face vs head is decided by geometry.** The nearest zone a ray strikes wins: a shot from the front
  reaches the face box first, one from behind the skull.
- **Own layer, no physics contacts.** Zones sit on the `Hitbox` layer, which collides with nothing; they
  exist only for raycasts (`HitReceiver.Raycast`, triggers included). Layer 8 is named `Player` because
  the Starter Assets player prefabs already use it.
- **`HitReceiver` is the only path into the sim.** It sits on the character root and turns
  (region, damage type, severity, origin) into an `InflictHit` command. The simulation chooses the wound
  definition, records the origin and applies damage, as before. Rejections (for example a character
  already down) are logged and returned, never thrown.
- **`SimCharacterLink`** says which simulation character a GameObject stands for (head of household or a
  fixed id). `CharacterMarksView` and `HitReceiver` both use it.
- **Debug stand-in for combat.** `DebugHitter` fires a real ray at the player from a random direction
  (cheat panel "Shoot me at a random spot", or F2), so the whole path is exercised before M2's ranged
  combat exists.

## Not done

- Weapons, projectiles and enemies (M2). Severity comes from the caller; there is no mapping yet from
  weapon, range or hit angle to severity.
- Zone sizes are greybox approximations for the Starter Assets mannequin; a real character model will
  need them checked.
- Hit reactions and ragdolls.

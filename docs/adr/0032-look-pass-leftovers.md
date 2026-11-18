# 0032 — Look-pass leftovers: props and clear grass

Status: accepted (built 2026-11-18 at Travis's request, while animations and characters wait; judged from
docs/style-frames/8-leftovers).

## Decisions

- **Remodelled in place** (`build_kit.py`; same names and footprints, so nothing that places them changes):
  - `Bush_A`, `Bush_B`: leaf-card bushes, no more solid blobs;
  - `Berry_Bush`: a card bush with red berry clusters;
  - `Fence_Rail`: an Appalachian split-rail run, posts and rough sagging rails;
  - `Salvage_Pile`: tin sheets, a tyre, boards, a crate, scrap;
  - `Hearth_FirePit`: a ring of fieldstones, crossed logs and flames, a tripod and pot, two log benches.
- **Grass stays clear** of anything solid at its roots (walls, fallen roofs, stones, trunks, thickets), using
  a physics check at build time, and stays 1.4 m away from every search spot so finds can be seen.

## Not done

Smaller props still on their older models: the kitchen, smokehouse, infirmary set, stores shed, job board,
woodpile and herb patch. The overpass deck and pillars are older kit models.

## Assets reviewed (Travis's account and the Asset Store's top free)

- **Ultimate Nature – Starter** (on the account, URP): stylised, faceted rocks with painted moss, cliffs,
  logs, stumps, mushrooms, flowers, spruces. It fits the ADR 0027 look. It's a candidate for cliffs, outcrops,
  rocks and forest-floor props, git-ignored like any store asset, with the builder falling back to our own
  models when it's absent. Not imported yet.
- **Adventure Character** (on the account): a realistic, rigged humanoid man in modern clothes, with outfit
  variants and 4K textures. A possible stand-in player through `UseHomesteader`. Not imported yet.
- **MapMagic 2 and MicroSplat** (on the account): both work on Unity Terrain, which the region doesn't use.
  MapMagic is worth a look if the world ever goes endless.
- **From top free:** Particle Pack (fire, smoke), War FX (muzzle flash, impacts), and the free animation
  packs (Human Basic Motions, the MoCap Central sample, RPG animations) look useful for later steps.

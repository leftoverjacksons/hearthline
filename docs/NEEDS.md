# What Claude needs from Travis

A running list of things only Travis can get: animations, reference images, installs and decisions. Claude
keeps it current. Tick items off (`[x]`) as you go, or tell Claude and it will. Newest needs go at the top of
each section. **Priority:** 🔴 blocking the current step · 🟡 needed soon · ⚪ later.

Nothing here costs money. When something would, it goes under "Purchases (on hold)" instead.

---

## Animations (Mixamo)

**Where:** `Assets/ThirdParty/Mixamo/` (git-ignored; Adobe's terms don't allow sharing the raw files).
**How:** mixamo.com → pick the clip → Download → Format **FBX for Unity**, Skin **Without Skin**, 30 fps.
Leave "In Place" unticked on walks. Any of the "8-way" packs gives every direction in one download.
Search terms are given where the exact Mixamo name varies.

### Fix first
- [x] 🔴 **T-Pose**: arrived 2026-11-18 as `X Bot@T-Pose.fbx`. Every clip now uses it as its body map.
  Mixamo's `Character@Clip.fbx` file names work as well as plain clip names.

### Movement and cover (next step)
- [ ] 🟡 Crouch walk left, right and backwards (search "crouched walk", "crouched sneaking left/right")
- [ ] 🟡 **Sticky cover set** (cover works now; these make it look right). Mixamo's cover clips vary in quality; grab whatever of these exists, and IK fills
  the gaps:
  - [x] standing with your back to a wall: idle, and sliding left and right. Taking Cover Idle and Left/Right
    Cover Sneak, wired into the player's animator on 2026-11-18. Check whether the feet match the slide:
    **Player → PlayerCover** has `swapSlideClips` if they run backwards, and `backToHighCover` to face the
    wall instead.
  - crouched behind low cover: idle, and sliding left and right (search "crouch cover", "crouched cover")
  - leaning out from the edge to aim, left and right (search "cover peek", "lean", "cover to aim")
  - rising over low cover to aim (search "crouch to aim", "pop up")
  - entering and leaving cover (search "to cover", "cover to stand")
- [x] 🟡 **Rifle aiming idle**: wired 2026-11-18. Shouldered while aiming, Rifle Idle and Walk With Rifle
  lowered at the ready. Check that the gun lines up with the crosshair.
- [ ] 🟡 **Pistol aiming idle** (search "pistol idle"): Pistol Aim is still a stand-in
- [ ] 🟡 Rifle aimed walk in all directions (search "rifle strafe", or a rifle 8-way pack)
- [ ] 🟡 Rifle run
- [ ] ⚪ Vault over a low obstacle (search "vault", "jump over")

### Shooting and the ambush (step after)
- [ ] 🟡 Firing a rifle, standing and crouched (search "firing rifle", "shooting rifle")
- [ ] 🟡 Pistol (revolver) firing (search "shooting", "pistol shoot")
- [ ] 🟡 Rifle reload (search "reloading")
- [ ] 🟡 Hit reactions: two or three, front and back (search "hit reaction")
- [ ] 🟡 A death or two (search "dying", "death from front/back")
- [ ] ⚪ Draw and holster the pistol (search "draw pistol", "holster")
- [ ] ⚪ Wounded walk or limp, for the injury Marks (search "injured walk")

### Homestead (nice to have)
- [ ] ⚪ Chopping wood, digging, hammering, carrying a box, sitting, sleeping, drinking

### Have
- [x] Crouching (down), Crouching Idle, Crouched Walking, Crouch To Stand, Crouch Cover Turn
- [x] Gathering Objects
- [x] Rifle Idle, Walk With Rifle, Walk With Rifle lowered, Put Back Rifle Behind Shoulder
- [x] Pistol Aim

## Characters

- [x] 🟡 **The Tripo character**: `femaleTest1` arrived 2026-11-18. She's now the player (ADR 0035). To bring in another one, run `ArtSource/Characters/export_tripo_character.py` (the command is at the top of the script), then set `SceneBuildKit.PlayerCharacter`.
- [ ] 🔴 **Tripo licence**: which plan was she made on? Her files are git-ignored until we know whether the public repo may hold them.
- [ ] ⚪ **Modular character pieces** (later): the backpack is part of her mesh. Separate meshes for the pack, hat and coat would let gear show on the body.

---

## Reference images

**Where:** `ArtReference/<folder>/` (git-ignored; see `ArtReference/README.md`). A one-line note per image in
the folder's `notes.md` saying *what* you like about it helps more than the image.
Generated images are fine; prompts are in `docs/art/reference-prompts.md`.

- [ ] 🟡 **Trees** → `places/trees/`. The leaf-card oak (ADR 0020) uses a generic leaf; references let the other
  tree types match real ones. Whole trees and close-ups of the leaves and bark: live oak with Spanish moss,
  loblolly pine, sweetgum, tulip poplar, dead snags, trees smothered in kudzu. A note on how dense or open you
  want the canopy.
- [ ] 🟡 **Characters: the look you want** → `people/`. MPFB gives a realistic base body that can be restyled
  (proportions, faces, flat or painted materials to match the world). A few images of characters from games
  or art whose *style* you like (not just costume), with a note on what you like, would say which way to push it.
- [ ] 🟡 **Cover objects** → `places/`. What people hide behind out here: car wrecks, stone walls, stumps,
  hay bales, collapsed porches. Shapes and scale matter more than detail.
- [ ] ⚪ **Gear** → `gear/`. Lever rifle, bolt rifle, revolver, shotgun, a homemade bow, knives; worn and
  repaired. Needed when weapons get models (shooting step).
- [ ] ⚪ **Marks** → `marks/`. Scars, bandages, burns, how far realism goes (M1 visuals).
- [ ] 🟡 **Animals** → `animals/`. Whitetail deer first (Crookhorn is a greybox buck right now), then boar and wolves.
- [ ] 🟡 **Play the hunt** (Job board → Crookhorn) and say how it feels: how far he sees and hears you, how the trail reads, whether 6 signs is too many.
- [ ] ⚪ **Not this** → `not-this/`. Any looks to avoid.

### Have
- [x] `people/`: three homesteader turnarounds
- [x] `places/`: nine scenes (creek homestead, dawn fog, rainy hearth, the wrong forest, Elder Works…)
- [x] `style/`: rain on the overgrown highway

---

## Installs

- [x] **MPFB** extension (v2.0.18, installed)
- [ ] 🟡 **MPFB asset pack** ("makehuman system assets", CC0), for the real player character:
  1. In Blender, in Object Mode, hover the mouse over the 3D view and press **N**. A sidebar opens on the
     right edge with vertical tabs (Item, Tool, View, …). Click the tab labelled **MPFB v2.0.18**.
  2. In its **Start here** panel, under SYSTEM ASSETS, click **Get system assets**. It opens the download
     page in your browser. Download the zip and don't unzip it.
  3. Back in that panel, click **Install pack from zip** and pick the zip (yours downloaded to
     `Assets/makehuman_system_assets_cc0.zip`; it's git-ignored, and can be deleted once installed). Blender may freeze for a few
     seconds.
  4. Restart Blender (and its MCP add-on), then tell Claude.
- [x] **.NET 8 SDK** (headless tests run: `tools/test.sh`)
- [ ] 🟡 **Tripo DCC Bridge for Blender** (Tripo's add-on, which sends a model from your Tripo account straight into Blender). Install it from Tripo's site in Blender (Edit → Preferences → Add-ons → Install from disk) and sign in with your own account. Then the export script can start from what the bridge brings in, rather than an FBX download. Signing in is yours to do.

---

## Decisions waiting on you

- [x] **Approve the life-and-weather spec** (`docs/specs/life-and-weather.md`): weather in the simulation,
  birds, insects and fireflies around you, and birds going silent in Thin places.
- [x] **OK to download CC0 sounds?** (yes: OpenGameArt CC0, credits in `Assets/Hearthline/Art/Audio/CREDITS.md`) Rain, wind, birdsong, insects and thunder from a free CC0 library
  (Freesound CC0 or the Sonniss GDC bundles). Until then, the audio sources are in place but silent.
- [x] **Approve the look-pass spec** (approved 2026-11-18: Valheim, no photo scans, our own models, forest; ADR 0027) (`docs/specs/look-pass.md`): one look in the family of Valheim, The
  Long Dark and Medieval Dynasty, judged from fixed screenshots after each step. Three questions at the end:
  which reference is closest, dropping the photo-scanned direction, and our own Blender kit vs free packs.
- [x] **Approve the ruins-and-places spec** (approved 2026-11-18 with Claude's picks) (`docs/specs/ruins-and-places.md`): place kinds, searching that
  stays searched, place events, and the Elder Works site. Three questions at the end: does salvage come back,
  a salvage goal at the job board now or later, and OK to download one CC0 hum for the Elder Works?
- [ ] 🟡 **Play a trip** (the M2 gate: is one out-and-back trip fun in greybox?). Weather and sound are in;
  their volumes are first guesses on the `Weather` and `Ambient Life` objects in M2_Region.
- [x] Commit homestead, shading, crouch and leaf-card oak (7b844a9)
- [x] Approve the hunt spec: a deer first, from the job board
- [x] Approve the combat and ambush spec (docs/specs/combat-and-ambush.md), with sticky cover
- [x] Leaf-card oak vs the old blob trees (settled by ADR 0027: modelled low-poly Appalachian trees replace both)
- [ ] 🟡 **Play a fight and tune its feel.** Cheat panel (F1), Combat tab: "Start reavers_road here". Or at the
  ford, refuse the toll and draw. These are all starting guesses, in the Inspector:
  - aim: camera distance, field of view and aim speed on the Player (AimController);
  - guns: spread, recoil and draw time (PlayerWeapon);
  - cover: reach, slide speed and lean distance (PlayerCover);
  - enemies: accuracy, hide and expose times (Enemy_Reaver prefab, EnemyAI);
  - the quick-draw slowdown (Fight Director);
  - the gun stats in `Content/items/*.yaml`.
- [ ] ⚪ Tune the look values in ADR 0018 by eye (they are starting points)
- [ ] ⚪ **Walk the new grass and tune it** (ADR 0033). Select **Grass** in the M2_Region scene: density, reach, blade size, colours, wind and how far the player pushes it are all on `GrassField`. Changes there are live in Play Mode, but they reset when Play Mode ends; to keep a value, change it outside Play Mode. Rebuilding the scene resets them all to the code defaults, so tell me the values you like and I will make them the defaults.
- [ ] ⚪ **Walk through tall grass and judge the matting and paths** (ADR 0034).
  - **Matting** is on **Trample**: how long grass takes to stand back up, the size of the pressed oval, and how hard the player, deer and enemies press. How far blades lie over is `trample` on **Grass**.
  - **Path colour** is the dirt colour on **Homestead → Wear**.
  - **Path edges** are on the `Region_Ground` material: edge sharpness and breakup.
  - **Seeing paths quickly:** the F1 World tab can show wear in red, wear in your last 100 m, clear all wear, or age it by 30 days.
  - Paths are one metre wide. Tell me if they should be wider or softer.

---

## Purchases (on hold)

Nothing is being bought for now. Things the design expects to buy eventually, so the free stand-ins can be
swapped later: character pack, foliage and environment packs, animation packs, audio.

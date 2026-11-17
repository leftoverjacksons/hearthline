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
- [ ] 🔴 **T-Pose** (search "T-Pose", same settings, save as `T-Pose.fbx`). Skeleton-only clips need it as
  their shared body map, or poses come out bent (ADR 0019). Unity picks it up by itself when it lands.

### Movement and cover (next step)
- [ ] 🟡 Crouch walk left, right and backwards (search "crouched walk", "crouched sneaking left/right")
- [ ] 🟡 **Sticky cover set** (cover works now; these make it look right). Mixamo's cover clips vary in quality; grab whatever of these exists, and IK fills
  the gaps:
  - standing with your back to a wall: idle, and sliding left and right (search "cover", "wall", "back against")
  - crouched behind low cover: idle, and sliding left and right (search "crouch cover", "crouched cover")
  - leaning out from the edge to aim, left and right (search "cover peek", "lean", "cover to aim")
  - rising over low cover to aim (search "crouch to aim", "pop up")
  - entering and leaving cover (search "to cover", "cover to stand")
- [ ] 🟡 **Rifle aiming idle** and **pistol aiming idle** (search "rifle aiming idle", "pistol idle"): the
  aim layer uses Rifle Idle and Pistol Aim as stand-ins, so the arms don't hold the shouldered gun yet
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
- [ ] ⚪ **Animals** → `animals/`. A boar (the first hunt), deer and wolves, if you have a look in mind.
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

---

## Decisions waiting on you

- [x] Commit homestead, shading, crouch and leaf-card oak (7b844a9)
- [ ] 🟡 **Approve the hunt spec** (`docs/specs/hunt.md`): the last piece of M2. Three decisions at the end.
- [x] Approve the combat and ambush spec (docs/specs/combat-and-ambush.md), with sticky cover
- [ ] 🟡 Leaf-card oak vs the old blob trees: convert the rest, or change direction? (ADR 0020)
- [ ] 🟡 **Play a fight and tune its feel.** Cheat panel (F1), Combat tab: "Start reavers_road here". Or at the
  ford, refuse the toll and draw. These are all starting guesses, in the Inspector:
  - aim: camera distance, field of view and aim speed on the Player (AimController);
  - guns: spread, recoil and draw time (PlayerWeapon);
  - cover: reach, slide speed and lean distance (PlayerCover);
  - enemies: accuracy, hide and expose times (Enemy_Reaver prefab, EnemyAI);
  - the quick-draw slowdown (Fight Director);
  - the gun stats in `Content/items/*.yaml`.
- [ ] ⚪ Tune the look values in ADR 0018 by eye (they are starting points)

---

## Purchases (on hold)

Nothing is being bought for now. Things the design expects to buy eventually, so the free stand-ins can be
swapped later: character pack, foliage and environment packs, animation packs, audio.

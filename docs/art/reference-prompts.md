# Art reference prompts

Reference-image prompts and asset briefs taken from `docs/DESIGN.md` (sections *Art, audio and feel
direction*, *The body as record*, *Homestead and household*, *Gear*) and from the content in `Content/`.

Who this is for:

- **Image generation.** Each prompt can be pasted into a text-to-image model to produce mood and
  reference images. Prepend the style block named in the section header.
- **Asset agents (Blender, Substance, Krita, etc.).** Sections marked **Brief** list deliverables.
  The `visual` ids in `Content/marks/*.yaml` are the keys the Bridge will look up, so deliverables
  are named after them.

## Read this first

1. **Most of this is reference, not a build order.** The design says to buy environments, foliage,
   props, modular buildings, animals and animation (*Asset strategy*). Environment, building and
   creature prompts exist to **pick and conform** Asset Store packs, not to model them from scratch.
   The design lists four kinds of custom work: **mark decals, exposure materials, the family chronicle
   UI and key storybook illustrations.** The briefs in sections 4, 9 and 10 cover those.
2. **The character pack is not chosen yet** (M0 is still open). Decal UV placement, blendshape names
   and part-swap meshes depend on it. Until then, author decals as standalone texture sets on a
   neutral canvas (section 4) and treat any placement as provisional.
3. **The prompts describe qualities, not other games.** The design's references (The Witcher 3's
   foliage density, RDR2's light, Ghibli-like greenery) are written out here as visual qualities.
   Naming studios or titles in a prompt tends to produce imitations, and the game needs its own look.
4. **The strange is shown through light, sound and wrongness, never creature designs** (*Visual
   direction*). No prompt here depicts the Neighbors. Do not add one.
5. **Elder Works and folklore.** The design asks that real Indigenous traditions be researched and
   handled with care, or replaced with invented equivalents. Southeastern mounds are real Indigenous
   earthworks. The Elder Works prompts use invented geometry and avoid reproducing real sites or
   Mississippian iconography. Keep it that way unless that decision is made explicitly.
6. **Hex values below are proposals.** The design fixes the palette in words only. Treat the hex codes
   as a starting point for Travis to tune, not as a decision.

Priority order for M1 (*Body as record*): section 4 (marks), then 3 (characters), then 2 (palette and
light). Everything else can wait for M2–M5.

---

## 1. Style blocks

Prepend one of these to every prompt in a section that names it.

**STYLE-3D** (in-game look, used for key art, environments, characters and props)

```
painterly realism, stylized-realistic 3D game art, slightly simplified shapes and broad hand-painted
textures, rich saturated color, strong atmospheric perspective, volumetric light, dense layered
foliage, humid overgrown American Southeast, Piedmont and Appalachian foothills, post-collapse
frontier ninety years after, worn handmade and salvaged materials, cinematic third-person framing
```

**STYLE-THIN** (append to STYLE-3D for anything in or near a Thin place)

```
desaturated cold palette, grey-green and slate, single unnatural accent of pale blue-violet light,
still air, no birds, subtle wrongness: a shadow falling the wrong way, a repeated tree or stone,
light that does not flicker
```

**STYLE-STORYBOOK** (interlude and off-screen event panels; the panel style itself is still to be
decided, this is a first candidate)

```
illustrated storybook panel, flat layered gouache and ink, limited palette, visible paper texture,
simple readable silhouettes, figures small in the landscape, faces simplified but with scars and
marks clearly drawn, horizontal panel composition, no text
```

**STYLE-SHEET** (orthographic reference sheets for modelers)

```
orthographic reference sheet, neutral grey background, flat even studio light, no perspective
distortion, front side back views, scale bar, clean silhouette, no motion blur, no depth of field
```

**Negative prompt** (all sections, where the generator supports one)

```
photorealistic skin pores, hyperdetailed, plastic, glossy, neon, cyberpunk, sci-fi armor, gas masks,
zombies, monsters, aliens, tentacles, desert wasteland, rust-orange apocalypse, text, watermark,
logo, UI
```

The negative prompt matters. Generators default to the orange, dusty wasteland look of post-apocalyptic
fiction. Hearthline is green and wet, and civilization "thinned out and went green".

---

## 2. Palette, light and mood frames

Style: STYLE-3D (and STYLE-THIN where noted).

### Proposed palette (to be tuned)

| Role | Proposal | Notes |
| --- | --- | --- |
| Home green | `#4F7A2E` `#7FA33A` | Saturated, warm green near home |
| Home gold | `#D9A441` `#F0C76A` | Late sun, straw, lamplight |
| Hearth | `#E07B39` | Interior fire and windows at dusk |
| Wet earth | `#5A4630` | Mud, bark, tanned leather |
| Thin base | `#7C8A86` `#4E5A5E` | Cold, desaturated grey-green |
| Thin accent | `#B9B4F0` | The single unnatural pale blue-violet |
| Continuity grey | `#6E7275` | Coats, crates, depots |

### Mood frames

**P-01 Homecoming at dusk** (the design's named key image)
```
a rider leading a pack horse loaded with pelts up a muddy track toward a small homestead at dusk,
two cabin windows lit warm orange, smoke from a chimney, fireflies over a creek, blue hour sky with
the last gold on the treetops, the homestead small against a wall of dark green forest, feeling of
arrival and safety
```

**P-02 Near-home midday**
```
summer meadow running down to a clear creek, split-rail fence half swallowed by vines, tall grass and
goldenrod, oak and tulip poplar forest edge, cumulus clouds, saturated greens and golds, a distant
cabin, heat haze, insects in the light
```

**P-03 Hollow fog at dawn**
```
deep wooded hollow at dawn, volumetric fog lying in the low ground, rhododendron thickets, mossy
boulders, a game trail, shafts of cold light through the canopy, wet leaves, quiet
```

**P-04 Rain on the road**
```
collapsed four-lane interstate used as a dirt road, cracked asphalt under moss and kudzu, a fallen
overpass, a lone traveler in an oilskin coat and wide hat walking in steady rain, puddles, grey sky,
green everywhere
```

**P-05 Storm over the river crossing**
```
wide brown river at a shallow ford, willows on both banks, a rope strung across the water, storm
clouds building, wind in the trees, a flat-bottomed ferry pulled up on the gravel bar
```

**P-06 Edge of the Thin** (STYLE-THIN)
```
the place where ordinary forest turns: in the foreground warm green woods, in the background the
same trees but grey, still and colorless, a faint pale blue-violet glow low between the trunks,
a line of identical birch trunks repeating, a shadow on the ground pointing toward the sun
```

**P-07 Inside the Thin** (STYLE-THIN)
```
an old-growth forest clearing where everything is too still, mist hanging without moving, a single
steady point of pale blue-violet light at head height between two trunks, no lantern, no source,
a dropped hat on the ground, crickets absent
```
(This is the image for `Content/events/light_in_the_trees.yaml`.)

**P-08 Warm interior**
```
inside a log cabin at night, a family eating at a long plank table, oil lamps and a stone hearth,
a rifle on pegs over the door, drying herbs, a child asleep on a bench, deep warm light and soft
shadow, rain on the window
```

**P-09 Winter interlude**
```
the same homestead in deep winter, snow on the roofs, woodsmoke, a figure splitting wood, bare
hardwoods, low pink sun, feeling of time passing
```

Use P-02 and P-09 with the same camera angle to test how seasons read.

---

## 3. Characters

Style: STYLE-3D for portraits and scenes, STYLE-SHEET for turnarounds.

Characters are a modular human with layered clothing: base, mid, outer, head, hands, feet,
accessories (*Visible gear*). Every equipped item is visible on the model. These prompts are for
choosing a character pack and for conforming it.

**C-01 Homesteader, adult woman, turnaround** (STYLE-SHEET)
```
adult woman homesteader in her thirties, linen shirt, wool vest, canvas trousers, oilskin duster,
wide-brimmed felt hat, leather work gloves tucked in belt, lever-action rifle slung across the back,
revolver holstered on hip, sheathed knife, canvas satchel, rolled bedroll strapped high on back,
practical boots, clothes mended and patched, weathered but not ragged
```

**C-02 Homesteader, adult man, turnaround** (STYLE-SHEET)
Same as C-01 with `adult man`. Keep identical gear so the two can be compared layer by layer.

**C-03 Outfit layer breakdown** (STYLE-SHEET)
```
exploded view of one outfit as separate layers on a mannequin: base layer, mid layer, outer coat,
hat, gloves, boots, belt with holster and knife, satchel, bedroll; each piece labeled by position only,
same character proportions
```

**C-04 Wear states**
```
the same canvas coat shown four ways side by side: clean and mended, muddy to the knees, blood-stained
at the shoulder with a torn sleeve, soaked dark with rain
```
Washing and mending restore wear; this sheet is the target for the clothing wear shader.

**C-05 One life, four ages**
```
the same person shown at 16, 30, 50 and 70, standing in a row, same face structure and eye color,
at 50 grey at the temples, at 70 fully grey and stooped with a cane, clothing changing over time,
a scar on the left cheek appearing at 30 and fading but present at 70
```
Life stages are Youth, Adult, Elder, Old. The age marks are `age_grey` (`hair_grey`) and `age_stiff`
(`posture_stoop` 0.5).

**C-06 Three generations, family resemblance**
```
family portrait on a porch: a grandmother, her son, his teenage daughter, strong shared features
(same nose and brow), the grandmother with a missing eye and an eyepatch, the son with a burn scar
on one hand, the daughter with faintly glowing pale eyes, an old lever rifle held by the son
```
This is the lineage pillar in one image: shared blood, distinct marks, one heirloom.

**C-07 Calvary Ford townsfolk lineup** (STYLE-SHEET)
```
lineup of six frontier townsfolk: shopkeeper in apron and sleeve garters, doctor with a black bag,
gunsmith in a leather apron, preacher in a worn black coat, a farm wife, a boy running errands,
practical clothes built from homespun and salvaged old-world fabric
```

### Factions

**C-08 Homesteaders** — C-01 and C-02 are the baseline. Faction read: practical, patched, a charm or
two against the Thin (a nail in a hat band, a string of buttons).

**C-09 Continuity patrol**
```
four people in identical grey wool greatcoats of the same cut, peaked caps, old government insignia
worn smooth, one carries a ledger and a fountain pen, well-maintained bolt-action rifles, canvas
gaiters, standing in a line on a dirt road, orderly and out of place
```
From `Content/events/continuity_patrol.yaml`.

**C-10 The Open, a seeker**
```
a barefoot traveler at a farm gate at night, coat too thin for the cold, calm eyes, a few small
stones and feathers tied into their hair, carrying nothing, lantern light on their face
```
From `Content/events/seeker_at_the_gate.yaml`.

**C-11 Reavers at the ford**
```
a man sitting on a rope strung across a river ford, mismatched salvaged armor of leather and road
signs, a sawn-off shotgun across his knees, grinning, more figures half hidden in the willows behind,
wet afternoon light
```
From `Content/events/reaver_toll.yaml`.

**C-12 A Changed person**
```
a middle-aged woman at a market stall, patches of glassy translucent skin on her neck and the back
of one hand with faint veins of light beneath, eyes a pale luminous grey, townsfolk keeping a careful
distance, she is calm and ordinary otherwise
```
Changed people are "feared, revered or hunted depending on where you are". Do not make them monstrous.

---

## 4. Marks (M1 priority)

This is the system the game is built around. Each entry gives a **reference prompt** (for
look-development) and a **Brief** (deliverables). The `visual` key in each mark's YAML is the id the
Bridge will look up. Rule 3 of *The body as record*: marks are seen, on the model, in the character
sheet and in the mirror moment.

### 4.0 Decal deliverable spec (all decals)

**Brief, applies to every `decal:` id below.** Proposed, to be confirmed once the character pack is
chosen:

- Decals are **baked into a per-character skin texture layer**, not projected at runtime.
- Per decal, deliver: albedo tint with alpha mask, tangent-space normal, roughness, and a height or AO
  mask. PNG, 16-bit for normal and height. Power-of-two square canvas.
- Author on **neutral mid-tone skin** and test on at least three skin tones (light, medium, deep).
  Scar tissue reads differently across tones (paler on deep skin, pinker on light skin); the albedo
  must be a tint layer that adapts, not a painted-in skin color.
- Keep the decal orientation-free (no baked light direction), so it can sit on any body region the
  mark allows (`regions:` in the YAML).
- Name files `<decal_id>_<map>.png`, for example `scar_claw_albedo.png`.
- Target from the design: **12 simultaneous visible marks per character** without meaningful frame
  cost. That argues for one composited skin layer, not 12 materials.

### 4.1 Wounds (5 kinds × 3 visual stages)

Each wound has `fresh`, `bound` (used by the stabilized and healing stages) and `infected` decals.
Treatment decides whether it becomes the clean scar or the bad scar.

| Wound | Decals | Clean scar | Bad scar |
| --- | --- | --- | --- |
| `wound_gunshot` | `wound_gunshot_fresh/_bound/_infected` | `scar_gunshot` | `scar_ragged` |
| `wound_blade` | `wound_blade_fresh/_bound/_infected` | `scar_blade` | `scar_ragged` |
| `wound_claw` | `wound_claw_fresh/_bound/_infected` | `scar_claw` | `scar_ragged` |
| `wound_burn` | `wound_burn_fresh/_bound/_infected` | `scar_burn` | `scar_burn` |
| `wound_blunt` | `wound_blunt_fresh/_bound/_infected` | `scar_knit_bone` | `scar_crooked_bone` |

**M-W1 Wound progression sheet** (STYLE-SHEET, one per wound kind; swap the wound description)
```
medical-illustration-style progression of a single gunshot wound on a forearm, five panels left to
right: fresh entry wound with bleeding, bound with a linen field bandage spotted with blood,
infected with angry red swelling and yellow discharge under a dirty bandage, healed as a clean round
puckered scar, healed badly as a ragged raised scar; stylized painterly rendering, not gory,
readable at game distance
```
Wound descriptions to swap in:
- blade: `a straight knife cut along the forearm`
- claw: `three parallel claw rakes from a wolf`
- burn: `a burn across the back of the hand, blistered`
- blunt: `a broken forearm, purple bruising and swelling, splinted with sticks and cloth when bound`

Keep the gore level **readable, not graphic**. The game wants the player to remember a fight, not
look away from it.

### 4.2 Scars (7 decals, 2 blendshapes)

**M-S1 Scar library sheet** (STYLE-SHEET)
```
reference sheet of healed scars on neutral skin, six swatches labeled only by position: a thin pale
straight blade scar, a round puckered bullet scar with a small exit scar, three parallel claw scars,
a shiny tight burn scar with irregular edges, a wide ragged raised scar with stitch marks, a faint
surgical line over a straight healed bone; shown on light, medium and deep skin tones
```

| Mark | Visual | Reference note |
| --- | --- | --- |
| `scar_blade` | `decal: scar_blade` | Thin, straight, pale. Reads at a distance on the face. |
| `scar_gunshot` | `decal: scar_gunshot` | Round, puckered. Optional small exit scar variant. |
| `scar_claw` | `decal: scar_claw` | Three or four parallel lines. The boar and wolf events cause these. |
| `scar_burn` | `decal: scar_burn` | Glossy, tight, irregular. Lower roughness than skin. |
| `scar_ragged` | `decal: scar_ragged` | The "bad scar" for most wounds. Raised, uneven, stitch marks. |
| `scar_knit_bone` | `decal: scar_knit_bone` | Subtle. A set-straight bone may leave little or nothing visible. |
| `scar_crooked_bone` | `blendshapes: { crooked_bone: 1 }` | A visibly bent forearm, nose or finger. Needs a blendshape per region the pack supports. |

**M-S2 The face that tells a story** (STYLE-3D, close portrait)
```
close portrait of a weathered homesteader in late afternoon light, a pale blade scar through the left
eyebrow, a crooked nose that was broken and set wrong, a notch of burn scar along the jaw, calm
direct gaze, the scars integrated naturally into the face
```
Experience target from the design: "The scar on a character's face makes the player recall the fight
that caused it, unprompted." Use this to judge decal subtlety: visible at conversation distance,
not lurid.

### 4.3 Loss (part swaps)

| Mark | Visual | Reference prompt |
| --- | --- | --- |
| `loss_eye` | `part_swap: eyepatch` | M-L1 |
| `loss_fingers` | `part_swap: missing_fingers` | M-L2 |
| `loss_weak_arm` | `part_swap: arm_sling` | M-L3 |
| `loss_limp` | `part_swap: gait_limp` | M-L4 |

**M-L1 Eyepatch variants** (STYLE-SHEET)
```
four handmade eyepatches on a mannequin head: tooled leather with a thong tie, dark wool cloth wrapped
around the head, a salvaged piece of old-world fabric, and a bare healed eye socket with the lid sewn
closed and scar tissue around it
```
**Brief:** eyepatch mesh fitted to the pack's head, plus the healed-socket decal for when the patch
is off (mirror moment, bathhouse).

**M-L2 Missing fingers** (STYLE-SHEET)
```
the same right hand three ways: whole, missing the last two fingers at the second knuckle with smooth
healed stumps, missing the ring finger only; front and back views
```
**Brief:** hand mesh variants per side (`hand_l`, `hand_r`) and the grip animation that adapts. Glove
meshes need matching variants or a cut-finger glove.

**M-L3 Dead arm in a sling**
```
a homesteader with one arm held in a faded cloth sling knotted at the neck, the arm thin and limp, the
other hand carrying a revolver, coat worn over one shoulder only
```
**Brief:** sling mesh, arm pose override, coat that drapes over the slung arm.

**M-L4 Limp**
```
three-quarter view of a man walking with a pronounced limp on the left leg, a handmade wooden cane
with a carved grip, a leather and iron brace strapped around the knee
```
**Brief:** the visual id is `gait_limp`, so the main deliverable is an animation override, not a mesh.
The cane and brace are optional props. Animation choice stays with Travis.

### 4.4 Exposure (material parameters)

**Brief, all exposure marks:** these are shader parameters on the character's skin and eye materials.
The parameter names are already fixed by the content: `iris_glow`, `translucency`, `emissive_veins`.
Deliver a URP Shader Graph (or a sub-graph to merge into the pack's skin shader) exposing them as
0–1 floats, plus reference renders at each stage value used below.

**`exposure_pale_eyes`** — stages `faint` (`iris_glow: 0.3`), `settled` (0.2), `lit` (1.0).

**M-E1 Pale-lit eyes, three stages**
```
three extreme close-ups of the same eye: first with an iris faintly paler than normal and a barely
visible inner light, second slightly calmer and settled, third with the iris lit from within by a soft
pale blue-violet glow that lights the lower lid; eyelashes and skin otherwise natural
```
**M-E2 Pale eyes in the dark**
```
a woman's face in near-total darkness inside a barn, only her eyes catching and giving back a faint
pale blue-violet light, the rest of the face barely visible, unsettling but not hostile
```
Mechanically these grant night sight and unsettle townsfolk; the image should do both.

**`exposure_glass_skin`** — stages with `translucency: 0.4`, deepening to `translucency: 0.8,
emissive_veins: 0.5`.

**M-E3 Glassy skin, two stages**
```
close-up of the back of a hand and wrist with a patch of skin turned glassy and faintly translucent
like frosted quartz, the edges blending into normal skin, at a later stage the patch larger and clearer
with fine veins of pale blue-violet light visible beneath; beautiful and wrong at once
```

### 4.5 Rite

**`rite_house_brand`** — `decal: brand_raw` then `decal: brand_healed`.

The brand is "the mark the first of the family cut over the door" (`Content/events/brand_night.yaml`).
The family sigil itself has **not been designed**. It is the most reusable custom asset in the game:
brand, door carving, gun engraving, chronicle header.

**M-R1 Family sigil exploration** (STYLE-SHEET)
```
sheet of twelve simple folk sigils that could be carved with a knife into a door lintel and burned with
a blacksmith's iron: built from straight cuts and one curve, readable at thumb size, suggesting a
hearth, a line, a creek or a house; Appalachian folk-craft feeling, not runic, not occult, no letters
```
**M-R2 The brand, raw and healed**
```
a family sigil burned into the inside of a forearm, left panel freshly raw and blistered red, right
panel healed into a raised pale glossy scar years later; the same sigil carved into the wooden lintel
over a cabin door in the background
```
**Brief:** once a sigil is picked, one vector master, then the two decals, a door-carving texture and
an engraving stamp.

**`rite_guilt_shadow`** — has **no visual** in the content. See *Open items*.

### 4.6 Age

**M-A1 Greying ramp**
```
the same head of hair in five steps from dark brown to fully white-grey, temples greying first,
then streaks through, then salt and pepper, then white; painterly game-ready hair cards
```
`age_grey` drives `hair_grey` 0–1. **Brief:** hair and beard material ramp keyed to that one float.

`age_stiff` is `blendshapes: { posture_stoop: 0.5 }`. Use C-05 as the reference.

### 4.7 The M1 gate image

**M-G1 A person with a history** (STYLE-3D, the single most important prompt in this file)
```
waist-up portrait of a homesteader in their forties at a washbasin, shirt off, looking into a small
cracked mirror by lamplight: an eyepatch, three healed claw scars across the shoulder, a round bullet
scar low on the ribs, a raised family brand on the inner forearm, a crooked healed forearm, grey at the
temples, a glassy translucent patch on the back of one hand; every mark distinct and readable, the
person calm and tired, the scene quiet and private
```
The M1 gate: "a character carrying 6 or more marks reads as a person with a history in a single
screenshot." This also doubles as the reference for the mirror moment (washhouse).

---

## 5. Homestead

Style: STYLE-3D. These are reference for choosing and conforming a modular building pack. Building
uses fixed slots with upgrade tiers, so each structure needs its tiers to look like **one place
improving**, not three unrelated models.

**H-00 The starting plot**
```
a burned-out farm plot on a creek, the black frame of a house fallen in on itself, a stone chimney still
standing, a collapsed barn under honeysuckle, an old orchard gone wild, a cold creek, forest closing in
on three sides, morning light, hopeful rather than grim
```

**H-01 Hamlet at full growth**
```
a hamlet of a dozen people on a creek: a two-story farmhouse with a deep porch, a smokehouse, a forge
with a glowing door, a barn, a fenced orchard, a bathhouse, a ring of standing ward stones at the
treeline, laundry on a line, children and dogs, golden afternoon
```

Use H-00 and H-01 from the same camera position. The gap between them is the slow loop.

### Structure tiers

One prompt per tier, all from the same three-quarter angle on the same plot:

| Structure | Tier prompts |
| --- | --- |
| Dwelling | `a canvas wall tent on a wooden platform` → `a one-room log cabin with a stone chimney` → `a two-story frame farmhouse with a deep porch` |
| Hearth & kitchen | `an open stone fire pit with an iron tripod and kettle` → `a lean-to kitchen with a clay oven and work table` → `a log smokehouse with hams hanging and smoke seeping from the eaves` |
| Infirmary | `a cot under a canvas fly with a tin basin` → `a small sickroom with two beds and drying herbs` → `a surgery room with a scrubbed table, lamp and salvaged steel instruments` |
| Workshop | `a heavy workbench under a roof` → `a stone forge with bellows and anvil` → `a gunsmithy with racks of barrels, vises and a lathe driven by a treadle` |
| Tannery & larder | `a wooden rack with a stretched hide` → `a tannery with soaking vats, scraping beams and stretched pelts` |
| Stable | `a hitching rail with a water trough` → `a log barn with stalls, hayloft and tack wall` |
| Garden & fields | `a fenced kitchen garden` → `tilled fields of corn and beans` → `a young orchard of apple and peach trees` |
| Washhouse | `a tin tub behind a hung blanket` → `a small bathhouse with a woodstove, wooden tubs and a mirror` |
| Shrine / ward stones | `a small cairn of creek stones with a candle` → `a ring of tall standing stones carved with the family sigil, hung with charms` |

The washhouse holds the mirror moment. The ward ring should carry the family sigil (4.5) once designed.

---

## 6. World locations

Style: STYLE-3D unless noted. Vertical slice: homestead, Calvary Ford, river crossing, deep woods, one
Elder Works site, one Thin pocket.

**W-01 Calvary Ford, early**
```
a small rebuilt market town on a river: a dozen frame and log buildings along one muddy street, a
general store, a livery, a ferry landing, old-world ruins half absorbed into new walls, horses and
wagons, kudzu on the hills behind
```
**W-02 Calvary Ford, prosperous** — same angle, `a doctor's office, a gunsmith, a saloon with a lit
sign, a church, boardwalks, more people, goods stacked outside the store`.
**W-03 Calvary Ford, declining** — same angle, `boarded windows, a burned building, an empty street,
guards on a rooftop, odd chalk symbols on doors`.

Settlement values (Prosperity, Safety, Strangeness, Openness) are shown in the town. W-01 to W-03
should share a camera so the change reads.

**W-04 Kudzu town** — `a small old-world town completely swallowed by kudzu, the shapes of cars,
a water tower and a church steeple under green blankets, a path cut through`.

**W-05 Deep woods** — `old-growth hardwood forest, huge tulip poplars and hemlocks, fern floor,
fallen giant trunks, a legendary animal's trail`.

**W-06 Elder Works entrance** (STYLE-THIN, lightly)
```
a grassy earthen rise in the woods with an opening of fitted grey stone, the stones far older and more
precise than anything nearby, unfamiliar geometry with no recognizable culture's symbols, cold air
coming out, ferns growing at the edge
```

**W-07 The dry hall** (STYLE-THIN)
```
inside a buried stone hall, dry and quiet, light from a crack above, smooth walls of tight-fitted stone,
in a niche a loaf-sized stone that seems faintly warm, dust undisturbed
```
From `Content/events/the_humming_stone.yaml`.

**W-08 Grave with your name**
```
a single hand-squared fieldstone in deep woods, moss filling the carved letters of a family surname,
no other graves near it, late light, eerie stillness
```
From `Content/events/grave_with_your_name.yaml`. Leave the surname illegible in the reference.

**W-09 Continuity depot** — `a fenced compound of old prefab buildings and shipping containers kept
painted grey, a radio mast, a ration line, out of place in the green`.

---

## 7. Gear and items

Style: STYLE-SHEET for weapons and wearables, STYLE-3D for in-context shots.

**G-01 Weapons lineup**
```
lineup of frontier firearms in worn condition: a lever-action rifle, a bolt-action rifle, a
double-barrel shotgun, a single-action revolver, a recurve hunting bow with a hide quiver, a handmade
pipe-gun salvaged oddity; wood stocks oiled and dinged, steel blued and wearing silver at the edges
```

**G-02 One rifle across three owners** (heirloom)
```
the same lever-action rifle three times: new-ish in the first generation, then with a repaired cracked
stock bound in brass wire and initials carved in, then decades later with a worn smooth grip, a name
engraved on the receiver and the family sigil inlaid in the stock
```
Item history and engravings are what make an heirloom. This is the reference for the engraving system.

**G-03 Weapon condition** — `the same revolver clean, fouled with black residue, rusted and pitted`.

**G-04 Carried load**
```
a homesteader on foot carrying a wolf pelt over one shoulder, a rifle on the back, a heavy satchel,
leading a horse with a deer carcass across the saddle and a string of two more horses behind
```
Hauling home is gameplay; this is the target for carry poses.

### Item icons (for the 11 items in `Content/items/`)

Style suggestion for icons: `game inventory icon, single object, painterly, soft top light, neutral
dark background, slight drop shadow, centered`.

| Item id | Prompt |
| --- | --- |
| `bandage` | `rolled linen bandage, boiled and yellowed, tied with string` |
| `medkit` | `dented old-world metal first-aid tin, faded red cross, latch` |
| `cartridges` | `small cardboard box of brass rifle cartridges, some hand-reloaded` |
| `whiskey` | `brown glass bottle of homemade whiskey, cork stopper, paper label hand-lettered` |
| `jerky` | `strips of dark jerky wrapped in waxed cloth` |
| `boar_meat` | `a haunch of wild boar wrapped in cloth and twine` |
| `pelt_wolf` | `a folded grey wolf pelt` |
| `lamp_oil` | `a tin can of lamp oil with a screw cap` |
| `copper_wire` | `a coil of salvaged copper wire, greened in places` |
| `old_coin` | `a worn old-world coin, face rubbed nearly smooth` |
| `humming_stone` | `a smooth grey stone the size of a loaf with a faint pale blue-violet sheen in its grain` |

---

## 8. Animals and the strange

Style: STYLE-3D. Animals are bought assets; these are selection references.

**A-01 Wolves at dusk** — `three grey timber wolves at the edge of a field at dusk, eyes catching the
light, one in front`. (`Content/events/wolves_at_dusk.yaml`)

**A-02 Boar in the corn** — `a huge wild boar with tusks bursting out of a cornfield, stalks breaking,
mud-caked bristles`. (`Content/events/boar_in_the_corn.yaml`)

**A-03 Black bear** — `an old black bear at a creek, grey muzzle, a long healed scar across its flank`.

**A-04 Legendary variant**
```
a legendary white-maned boar much larger than normal, old scars and a broken-off tusk, a rusted
arrowhead still in its shoulder, standing in a misty hollow
```
Legendary animals carry unique pelts and leave unique marks; they should look marked themselves.

### The strange (no creatures)

Use STYLE-THIN. Each of these is a reference for a lighting or level trick, not a character.

| ID | Prompt |
| --- | --- |
| S-01 | `a sunlit field where one fence post's shadow points toward the sun` |
| S-02 | `a path through the woods where the same fallen log appears three times in a row` |
| S-03 | `a flock of birds sitting completely still on a power line, none moving, none singing` |
| S-04 | `a farmhouse window at night with a steady pale light inside that is not lamplight, curtains still` |
| S-05 | `smoke from a ridge fire rising straight up and then bending sideways at a hard angle` |

---

## 9. Storybook panels (custom work)

Style: STYLE-STORYBOOK. The design uses illustrated panels for interludes and for events too expensive
to stage. These are "key storybook illustrations", one of the four custom-work areas.

**Brief:** panels are generic so casting can fill them; figures are silhouettes or simplified faces.
Where a mark is central (an eyepatch, pale eyes), draw it, since event text references marks.
Proposed format: 16:9, layered PSD or Krita file so figures can be recolored per family.

One panel per current event:

| Event | Panel prompt |
| --- | --- |
| `wounded_stranger_creek` | `a figure slumped against a stone cairn at a creek at dusk, holding their side, a homesteader approaching on foot` |
| `wolves_at_dusk` | `three wolves at the edge of a field at dusk, a figure raising a rifle, lit cabin far behind` |
| `boar_in_the_corn` | `a boar charging out of a cornfield at a figure who is falling backward` |
| `smoke_on_the_ridge` | `a thin column of smoke on a far wooded ridge, a rider stopped on the trail looking at it` |
| `peddler_on_the_road` | `a peddler's wagon hung with pots and lanterns on a ruined highway, a mule, a figure talking` |
| `reaver_toll` | `a rope across a ford, a man sitting on it, shapes in the willows, a rider holding back` |
| `continuity_patrol` | `four figures in identical grey coats on a road, one writing in a ledger, a family watching from a porch` |
| `seeker_at_the_gate` | `a barefoot figure at a farm gate at night, lantern light, a homesteader at the door` |
| `child_on_the_road` | `a small child alone on an empty road at evening, a rider stopping` |
| `fever_in_the_house` | `a sickbed by candlelight, a figure keeping watch, a cloth in a basin` |
| `brand_night` | `a family around a hearth at night, an iron glowing in the coals, one person rolling up their sleeve` |
| `light_in_the_trees` | `a single steady pale blue-violet light low between dark trunks, a figure standing very still` |
| `the_humming_stone` | `a figure in a dark stone hall holding a stone close to their ear, faint light on their face` |
| `grave_with_your_name` | `a lone figure looking down at a mossy hand-cut gravestone in the woods` |

Interlude panels (no event file yet): `passing the season` (the homestead across four seasons in four
strips), `a birth` (a lamp-lit room, a family gathered, handled with dignity: see Open questions in the
design about how much is shown), `a funeral` (graves on a hill, the heir holding the rifle).

---

## 10. UI references (custom work)

Style: STYLE-3D for diegetic objects; no generator prompt for layout.

**U-01 Family chronicle**
```
a family tree drawn by hand across the pages of a large old ledger book, small painted oval portraits
for each person, scars and eyepatches visible in the tiny portraits, dates and short notes in ink,
some portraits with a black ribbon, the family sigil at the top of the page, lamplight on the paper
```
The design calls the chronicle "the payoff screen for the whole game". It shows every member's
portrait with their marks, deeds and cause of death.

**U-02 Portrait style** — `small painted oval portrait of a frontier woman with a scar through her
lip and pale glowing eyes, folk-art painting, cracked varnish`. Portraits must show marks; they are
regenerated as marks change.

**U-03 Minimal HUD mood** — `a third-person game screenshot with almost no interface: no health bar,
no damage numbers, only a small hand-drawn compass at the top edge`. The design asks for minimal,
diegetic UI and no floating damage numbers.

---

## Open items

Things this list exposed that are not decided in the design or content. They need a decision, not an
image.

1. **`knack_dowsing` and `rite_guilt_shadow` have no `visual`.** Rule 3 says marks are seen "on the
   model, in the character sheet, in NPC dialogue, and in a dedicated mirror moment". If these are meant
   to be seen only in the sheet and dialogue, that should be stated; if not, they need a visual
   (candidates from the design's own exposure list: nosebleeds for the Knack; for guilt, nothing on the
   body, perhaps a habit animation).
2. **The family sigil** (4.5) is not designed and is reused in several places.
3. **Storybook panel style** is not chosen. STYLE-STORYBOOK is one candidate.
4. **Character pack** is not chosen, so decal placement, blendshapes (`crooked_bone`, `posture_stoop`)
   and part swaps (`eyepatch`, `missing_fingers`, `arm_sling`, `gait_limp`) cannot be finalized.
5. **Palette hex values** in section 2 are proposals.

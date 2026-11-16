# Hearthline — Game Design Document

Nov 16, 2026 · @Jeffrey Travis King

## Vision

**Hearthline** (working title) is a third-person homestead RPG. You play one person and then their heirs, in an overgrown world remade by the Unveiling. Every journey leaves marks on bodies, on gear, on the homestead and on the family line, and those marks are visible, mechanical and remembered.

**Logline:** Build a home at the edge of a strange, green world, go out, come back changed, and pass it all to your children.

### Design pillars

Every feature has to serve at least one pillar. A feature that serves none gets cut.

1. **Marks that last.** Injuries, exposures and deeds permanently change a character's body, abilities and how others treat them. Every mark has a story and a mechanical effect.
2. **The drift.** Trips are goals you get pulled away from. Distractions, ambushes and hauling loot home are the content, not filler between missions.
3. **A home that remembers.** The homestead and its people grow, scar and recover with you. Coming home should feel like coming home.
4. **Lineage.** The protagonist is the family, not one person. People age, children inherit traits, gear, grudges and reputation, and the attachment carries over.
5. **The strange at the edges.** The world is grounded frontier near home and stranger the farther out you go. The phenomenon is felt and survived, never fully explained.

### Experience targets

- The scar on a character's face makes the player recall the fight that caused it, unprompted.
- A planned 20-minute hunt turns into a 90-minute trip the player tells a friend about.
- When a character dies, the player picks up their rifle as the heir and feels it.

### Non-goals

- No Manor Lords-depth settlement management. The town is a backdrop that reacts to you.
- No RDR2-scale map. A few dense regions instead of one vast one.
- No multiplayer, no full voice acting, no freeform melee combat system in v1.

## Player fantasy and core loops

You are a homesteader, then the homesteader's child, then theirs. Two loops run at once: a **fast loop** of trips out and back measured in days, and a **slow loop** of seasons and generations measured in years.

&#91;embedded content: core loops · fast trip loop and slow generational loop\]

The Encounter step is highlighted because that is where Marks are created. The other steps either set it up or carry its consequences home.

- **Fast loop (30–120 min):** prepare at home, outfit, go out with a goal, get pulled off it, survive what happens, haul the results home, and spend them on home and people.
- **Slow loop (interludes):** passing a season simulates the world, ages everyone, and produces births, deaths and town growth. Eventually an heir takes over and the fast loop resumes in a changed world.
- **Moment-to-moment:** riding, tracking, hunting, shooting, sneaking, talking, carrying, tending wounds, bathing, mending.

## Setting: the Unveiling

About 90 years ago the phenomenon stopped hiding. There was no announcement. For months, consensus reality failed in patches: grids went down, people Changed, and whole towns emptied. Civilization did not end in fire. It thinned out and went green.

### Where and when

- **Region:** the overgrown American Southeast. Piedmont and Appalachian foothills, kudzu-swallowed towns, rivers that matter again, collapsed interstates used as roads.
- **Era:** the third generation after. Old-world tech exists as salvage: rifles, tools, a working truck is a legend. Most travel is on foot or by horse.
- **Starting point:** a burned-out farm plot on a creek, a day's ride from the rebuilt market town of **Calvary Ford** (placeholder name).

### The phenomenon

Locals call them **the Neighbors**, the way old folk spoke of the Good Folk: politely and never by a real name. Design rules:

1. **Never explained.** No alien council, no lore dump. Characters hold competing theories; the game confirms none of them.
2. **It behaves like a control system** (after Vallée). It responds to attention, belief and ritual. It teaches, tricks and tests. It is not simply hostile.
3. **Strangeness scales with distance.** Near home it shows up as rumor and the odd light. In the **Thin** places (deep woods, old Works, certain hollows) physics, time and memory misbehave.
4. **Contact changes bodies.** This is the setting's link to the core mechanic. Exposure marks (eyes, skin, the Knack) are how the world writes itself onto your family.

### Setting vocabulary (placeholders)

| Term | Meaning |
| --- | --- |
| The Unveiling | The event, \~90 years ago |
| The Neighbors | The non-human presence(s) behind it |
| The Thin | Places where the phenomenon is strong |
| The Knack | Psionic aptitude, latent in some bloodlines, raised by exposure |
| The Elder Works | Ruins older than the old world: mounds, stones, buried halls (the Atlantean/old-race thread) |
| Changed | People visibly altered by exposure; feared, revered or hunted depending on where you are |

### Factions (first pass)

- **Homesteaders and Calvary Ford:** frontier people rebuilding. Practical, superstitious, wary of the Changed.
- **Continuity:** remnant of the old government, scattered depots, rationed tech. Wants to study the Changed.
- **The Open:** a movement that seeks the Neighbors out. Some members are wise, some are dangerous.
- **Reavers:** raiders working the roads and river crossings.

**Folklore note:** Southeastern settings brush against Cherokee and other Indigenous traditions (the Nunnehi, for example). Any use of real living traditions should be researched and handled with care, or replaced with invented equivalents.

## The body as record

This is the system the game is built around. A **Mark** is a permanent or long-lived change to a character, with a visible form, a mechanical effect and a story. Prove this system first. Everything else is easier if it works.

### Rules

1. **Every mark is mechanical and narrative.** No cosmetic-only scars. A burned hand slows reloads; a Changed eye sees in the dark and unsettles townsfolk.
2. **Every mark is named by its origin.** "Scar from the boar at Miller's Ford, spring of year 3." The name comes from the event that caused it.
3. **Marks are seen.** On the model, in the character sheet, in NPC dialogue, and in a dedicated mirror moment (barber, bathhouse, creek reflection).
4. **Marks can progress.** A wound becomes a scar if treated, an infection or a lost limb if not. An exposure can deepen or settle over time.
5. **Some marks are heritable.** Exposure echoes and the Knack can pass to children, sometimes skipping a generation.

### Mark categories

| Category | Examples | Typical effect | Heritable |
| --- | --- | --- | --- |
| Wound | Gunshot, claw, burn, broken bone | Temporary debuff until treated | No |
| Scar | Facial scar, burned hand, notched ear | Small permanent stat change; NPC reactions | No |
| Loss | Lost eye, missing fingers, limp | Large permanent change; adapted play style | No |
| Exposure | Pale-lit eyes, glassy skin patches, nosebleeds, voices | Strange gifts with costs; faction reactions | Sometimes (as an echo) |
| Knack | Sensing, nudging minds, dowsing the Thin | Psionic abilities; draws the Neighbors' attention | Yes, latent |
| Rite | Tattoos, brands, ritual cuts, a family ring | Faction standing, morale, identity | No (but kept as heirloom or custom) |
| Age | Grey hair, stiffness, weathering | Slower physicals; wisdom and teaching bonuses | No |

### Data shape (sketch)

```
Mark {
  id, defId              // instance id, and definition from content files
  category               // Wound | Scar | Loss | Exposure | Knack | Rite | Age
  bodyRegion             // Head, Face, Torso, ArmL, HandR, LegL, ...
  severity: 1..3
  stage                  // e.g. Fresh -> Healing -> Scarred, or Latent -> Waking -> Settled
  effects[]              // stat modifiers, ability grants, reaction tags
  originEventId, originText, dateGained
  heritable: none | echo | latent
  visual                 // decal id, blendshape weights, material params
}
```

### Visual implementation

- **Scars and wounds:** decals baked into a per-character skin texture layer (not runtime projectors), so they stay cheap and persist.
- **Exposure:** material parameters (emissive veins, iris glow, translucency) and a few blendshapes.
- **Loss:** mesh-part swaps (eyepatch, prosthetic, missing fingers) plus a gait or animation override layer where needed.
- **Age:** hair and skin material ramps, plus a posture blend on the locomotion set.
- Target: one character supports at least 12 simultaneous visible marks with no meaningful frame cost.

## Lineage, aging and succession

You control one **head of household** at a time. When they die or retire, you take over an heir. Their marks, gear, grudges and reputation stay in the world. A campaign targets **3 to 5 generations** over roughly 60 to 100 in-game years.

### How time passes

Real-time play cannot cover decades, so time moves in two modes:

- **Live time:** days of play during trips and at home. A full day/night cycle is about 40 real minutes.
- **Interludes:** at the homestead, the player chooses to **pass the season** (or winter over). The game simulates the gap: crops, births, illness, town growth, aging, household events. It then presents what happened as a short storybook sequence, the way Wildermyth does between chapters.

Target pacing: one in-game year takes about 2 to 4 hours of play, and a character's active adult life spans about 20 to 30 in-game years.

### Life stages

| Stage | Age (approx.) | Role |
| --- | --- | --- |
| Child | 0–12 | Lives at home; personality and traits form from household events |
| Youth | 13–17 | Can join short trips; learns from elders; picks up marks early |
| Adult | 18–45 | Fully playable; peak physicals |
| Elder | 46–65 | Slower physically; strong teaching and reputation bonuses; can retire |
| Old | 65+ | Mostly stays home; risk of natural death each interlude |

### Family and inheritance

- **Partners:** found among recruits, townsfolk and travelers through relationship events. No dating sim; a few meaningful beats.
- **Children:** born through interlude events. Each child gets traits from both parents with a chance of mutation, and can inherit an exposure **echo** or a latent **Knack**.
- **Heirlooms:** gear carries its history and can be bequeathed (see Gear).
- **Reputation and grudges:** factions and individuals remember your **surname**. A Reaver band your grandmother wronged still hunts your family.
- **Elders at home:** retired characters stay on as mentors, granting training and morale bonuses until they die.

### Succession

1. Triggered by death, a crippling loss, or voluntary retirement at Elder age.
2. The player chooses among eligible heirs: adult children, then adopted wards, then sworn household members.
3. No eligible heir means the line ends. Default: that ends the campaign, with a family-chronicle epilogue.
4. The new head takes over with a short transition scene: funeral or handover, the bequeathed gear, and the household's reaction.

### The family chronicle

A persistent family tree and chronicle UI is a core feature, not a menu. It shows every member's portrait with their marks, their notable deeds (auto-written from events) and their cause of death. This is the payoff screen for the whole game.

## Homestead and household

The homestead is an RDR2-camp-scale base that grows into a small hamlet of **6 to 12 people**. It is a support system for adventure, not a city builder. Building uses **predefined slots with upgrade tiers**, not freeform placement, which keeps scope and art cost bounded.

### Structures

| Structure | Tiers | What it does |
| --- | --- | --- |
| Dwelling | Tent → cabin → farmhouse | Household capacity; rest quality; family events |
| Hearth & kitchen | Fire pit → kitchen → smokehouse | Food, preservation, morale |
| Infirmary | Cot → sickroom → surgery | Wound treatment; whether a wound scars cleanly or worsens |
| Workshop | Bench → forge → gunsmithy | Repair, modify, engrave gear |
| Tannery & larder | Rack → tannery | Turns pelts and game into goods and trade |
| Stable | Hitch → barn | Horses, pack animals, carrying capacity |
| Garden & fields | Plot → fields → orchard | Food and trade over interludes |
| Washhouse | Tub → bathhouse | Hygiene; the mirror moment; small buffs |
| Shrine / ward stones | Cairn → warded ring | Protection from the Thin; Knack training |

### Household members

- Recruited through events, rescues, trade or marriage. Each has 2 to 3 traits, skills, relationships and their own marks.
- Assigned to **roles** (hunter, cook, healer, smith, farmer, guard) that produce results while you are away. Results are reported when you return.
- Can join trips as companions (maximum 2 in v1).
- Have opinions of the head of household and of each other. Low cohesion produces disputes, departures and theft.

### Home events

The home is a source of stories, not just a menu: raids while you are away, births, sickness, a stranger at the gate, a Neighbor sighting at the treeline, a marriage proposal, a feud between two members. They draw on the same event engine as the wilds (see Exploration).

### Downtime activities

These are lightweight activities that make home feel lived in: bathing and shaving, eating with the household, teaching a child, maintaining gear, and later card games at the Calvary Ford saloon. Each is short and optional, and each feeds a small system (morale, relationships, buffs).

## Exploration and the event engine

The world is a handful of dense, hand-built regions. A data-driven **storyteller** fills them with written events cast from whoever and whatever is present. Writing builds the events; the engine decides when they happen and who is involved.

### World structure

- **v1 / vertical slice:** one region of about 2 × 2 km: homestead, Calvary Ford, river crossing, deep woods, one Elder Works site, one Thin pocket.
- **Full game:** 3 to 4 regions, each 3 to 4 km across, with strangeness rising the farther they are from home.
- Each area carries **tags** (biome, danger, Thin level, faction control) that events query.
- Points of interest: ruins to loot, camps, legendary animals, shrines, wrecks, strangers' homesteads.

### The drift loop

1. The player picks a goal: a legendary animal, a salvage site, a trade run, a rescue.
2. On the way, the storyteller offers **distractions**: passersby, tracks, smoke on the ridge, a light in the trees.
3. Outcomes produce loot, marks, relationships and consequences.
4. **Hauling home is gameplay.** Carried items have weight and bulk. Pelts go over the shoulder or onto the horse, and you can lead captured horses in a string. Meat spoils. Loaded travel is slower and draws ambushes.

### Storyteller

The storyteller works like RimWorld's: it tracks a **tension budget** and paces events so quiet stretches and spikes alternate. Inputs: time since the last major event, party condition, distance from home, Thin level, faction heat, and recent player choices.

### Event schema

Events are content files (YAML), not code, so they are easy to write, diff and validate. Claude Code can generate and edit them in bulk against a schema with tests.

```yaml
id: wounded_stranger_creek
tags: [encounter, roadside, moral]
weight: 10
cooldown_days: 30
when:
  area_tags_any: [creek, road]
  thin_level_max: 1
  time_of_day: [dusk, night]
cast:
  hero: { from: party, role: head }
  stranger: { generate: traveler, traits_any: [wounded] }
beats:
  - text: "{stranger.name} is slumped against a cairn, holding their side."
    choices:
      - label: Help them
        requires: { item_any: [bandage, medkit] }
        outcome:
          relationship: { stranger: +30 }
          chance:
            - p: 0.7
              recruitable: stranger
            - p: 0.3
              ambush: reavers_small
      - label: Take their pack
        outcome:
          loot_table: traveler_pack
          reputation: { homesteaders: -5 }
          mark: { target: hero, def: rite_guilt_shadow, chance: 0.2 }
```

### Presentation

- **Diegetic first:** spawn the actors, stage the scene in the world, and present choices through a light dialogue wheel or prompt.
- **Storybook fallback:** for interludes and events too expensive to stage, use illustrated panels with text, as Wildermyth does. This is also how off-screen home events get reported.
- The text writes names, marks and history into the narration: "Ada, her bad hand aching in the cold, ..."

### Content targets

| Milestone | Events | Notes |
| --- | --- | --- |
| Event engine prototype | 20 | Cover every outcome type |
| Vertical slice | 40–60 | One region, home events included |
| Full game | 300+ | Chains, rare events, generational callbacks |

## Combat and stealth

Combat in v1 is **over-the-shoulder ranged combat**. Melee exists only as **stealth takedowns** using canned paired animations. Fights are short, lethal and consequential, because every hit can become a Mark.

### Ranged

- **Weapons:** revolvers, lever and bolt rifles, shotguns, bows, salvaged oddities. Ammunition is scarce and partly handmade.
- **Feel targets:** a deliberate pace like RDR2's, not Division-style bullet sponges. Enemies drop in 1 to 3 hits; so do you.
- **Weapon condition:** fouling, jams, wear. Maintenance at camp or in the field. Condition feeds the gear's history.
- **Cover:** contextual (lean and crouch behind objects), no sticky cover system in v1.
- **Quick-draw moment:** a short slowdown on ambush start, to deliver the "draw and drop them" fantasy without a full Dead Eye system.

### Stealth and takedowns

- Crouch, light and noise, and an enemy awareness meter.
- Takedowns are triggered from behind, or from cover on unaware targets. Paired animations: 3 to 4 to start (choke, knife, blunt, disarm).
- Non-lethal options matter for reputation and for capturing bounties alive.

### Hits become Marks

1. Each hit registers a **body region** (head, torso, arms, legs, hands).
2. A hit creates a **Wound** mark: bleeding, reduced function in that region.
3. Field treatment stabilizes it. Treatment at the infirmary decides the outcome: a clean scar, a bad scar, infection, or loss.
4. At zero health you are **downed**, not dead. Companions can revive you. Left unattended, it rolls for death or a permanent Loss mark, in the spirit of XCOM's bleed-out.

### Enemies

- **Human:** Reavers, rival homesteaders, Continuity patrols. Simple tactical AI: take cover, flank, flee when broken, surrender.
- **Animals:** wolves, boar, bear, and legendary variants with unique pelts and unique marks.
- **The strange:** rarely a fight. Usually an event with choices, sometimes a survival chase. Shooting the Neighbors should almost never be the right answer.

### Later (post-slice)

Full melee combat, horseback combat, and companion commands beyond follow, hold and attack.

## Gear, heirlooms and progression

Gear is always worn and seen, and it accumulates history. Characters improve through use and through events, not through XP levels.

### Visible gear

- **Every equipped item is on the model:** long gun across the back or on the saddle, sidearm holstered, knife, satchel, bedroll, hat, coat, layered clothing. Equipping the rifle you found shows it on your back, as in Witcher 3.
- **Outfit layers:** base, mid, outer, head, hands, feet, accessories. Clothing has warmth, weather protection, noise, and faction read (how people perceive you).
- **Wear shows:** mud, blood, tears, patches, wet cloth after rain. Washing and mending restore it.

### Item history

Each unique item keeps a **ledger**: who carried it, what it killed, notable events ("took a bullet meant for Ada at Miller's Ford"), repairs, engravings.

- **Naming:** after enough history, an item can be named and engraved at the workshop.
- **Heirloom bonus:** small, specific bonuses that grow with history and with blood relation to the original owner. For example, "Grandfather's Rifle: +steadiness for the Ward family."
- **Bequeathing:** on death or retirement, the player assigns key items to heirs, and the chronicle records it.

### Character progression

| Layer | Source | Example |
| --- | --- | --- |
| Skills | Grow by use, capped by age and marks | Marksmanship, Tracking, Medicine, Smithing, Stealth, Riding, Persuasion, Knack |
| Traits | Born with (inherited), gained or lost through events | Steady Hands, Hot-Tempered, Night Owl, Haunted |
| Marks | Injuries, exposures, rites, age | See The body as record |
| Reputation | Faction and personal standing, attached to the surname | Feared by the Reavers, trusted in Calvary Ford |

### Loot sources

Old-world salvage (tools, weapons, medicine, rare tech), animal products, trade, quest rewards, and rare Elder Works artifacts. Artifacts have strange properties and can leave exposure marks on the person who carries them.

## World simulation: settlements, factions, reputation

The world changes over the years in response to the family, mostly indirectly. Behind the scenes the simulation is a small set of numbers; in front, the player sees visible changes and named people.

### Settlement state

Each settlement (Calvary Ford first) tracks a few values that move during interludes:

| Value | Raised by | Lowered by | Visible as |
| --- | --- | --- | --- |
| Prosperity | Trade, cleared roads, good harvests | Raids, plague, isolation | New buildings, goods in stores, more people |
| Safety | Reavers driven off, patrols, walls | Unanswered raids, deaths | Guards, lit streets or boarded windows |
| Strangeness | Nearby Thin activity, Changed residents | Wards, purges | Odd lights, rituals, graffiti, fear |
| Openness | Changed people treated well | Lynchings, Continuity presence | How NPCs treat Changed members of your family |

Buildings and services unlock at thresholds: doctor, gunsmith, saloon, church or shrine, school, telegraph. Thresholds can also be crossed downward, so a town can decline.

### Persistent people

- Settlement NPCs are named, persistent and age, the same as your family. The shopkeeper's son can take over the shop, or marry into your line.
- NPCs keep a small memory list of notable interactions with your family, which events and dialogue draw on.

### Factions and reputation

- Reputation is tracked per faction and per **surname**, and decays slowly. Grudges are a special high-persistence entry that is inherited.
- Faction heat changes what the storyteller spawns: ambushes on the roads, Continuity agents asking about your Changed daughter, emissaries from the Open.

### Influence, not control

You never place town buildings. You shape outcomes by what you sell, who you protect, what you report, and who marries whom. The town's growth over decades is the long-term record of the family's influence.

## Art, audio and feel direction

Polish comes from **art direction, light, sound and feel**, not from raw fidelity. Choose a stylized-realistic look early and stick to it, because mixed asset styles are the most common reason solo 3D games look cheap.

### Visual direction

- **Look:** painterly realism. Slightly simplified shapes and textures, rich color, strong atmosphere. References: The Witcher 3's foliage density, the light of RDR2, and a touch of Ghibli-like greenery.
- **Palette:** saturated greens and golds near home. Desaturated, colder tones with one unnatural accent hue (pale blue-violet) in the Thin.
- **Lighting:** time of day, weather (rain, fog, storms), volumetric fog in hollows, warm interior light at home. Coming home at dusk to lit windows is a key image.
- **The strange:** shown through light, sound and wrongness (shadows in the wrong direction, silent birds, repeated geometry), not creature designs. Seeing too much ruins it.

### Asset strategy

- Buy, don't build, for environments, foliage, props, modular buildings, animals and the locomotion and animation base. Pick **one** environment art family and conform everything else to it with shared materials and post-processing.
- Budget real money for assets. For a solo developer, the Asset Store is the main lever on polish.
- Characters: a modular human system with swappable clothing layers and support for skin-detail layers (for marks). Evaluate candidate packs in M0 specifically for this.
- Custom work, done yourself or commissioned: mark decals, exposure materials, the family chronicle UI, key storybook illustrations.

### Audio

- Ambient-first: wind, insects, distant animals, creek, rain on canvas. Silence is a tool in the Thin.
- Music is sparse and acoustic (fiddle, banjo, guitar, low drones), swelling only for moments that matter. Diegetic music at home and in the saloon.
- Weapon audio gets disproportionate attention; it carries most of combat's feel.

### Feel checklist

- Third-person camera: framing, collision and shoulder swap.
- Footstep surfaces, foot IK on slopes, weight in starts and stops.
- Horse handling and leading a horse string.
- Hit reactions and ragdolls.
- Carrying animations (pelt on shoulder, body over the saddle).
- UI kept minimal and diegetic where possible; no floating damage numbers.

## Technical architecture (Unity 6)

The stack is Unity 6 with URP and C#. The key architectural decision is a **pure-C# simulation core** with no MonoBehaviours, kept separate from the Unity presentation layer. That keeps the systems that matter most (marks, lineage, events, world state) testable, saveable and easy for Claude Code to work on without the Editor.

### Stack

| Area | Choice | Note |
| --- | --- | --- |
| Engine | Unity 6 (LTS), URP | URP suits stylized lighting and performance; HDRP is overkill solo |
| Language | C# |  |
| Input | Input System package | Gamepad first, then mouse and keyboard |
| Camera | Cinemachine | Third-person follow, aim and cinematic event cameras |
| Animation | Mecanim + Animation Rigging | Foot IK, aim IK, carry poses; evaluate a third-party animation layer later |
| AI navigation | AI Navigation (NavMesh) |  |
| Content loading | Addressables | Regions and assets streamed by area |
| Content data | YAML files in the repo + ScriptableObjects for asset references | YAML for events and definitions; validated by schema tests |
| Save | Versioned JSON of the simulation state | The simulation is the source of truth; scenes are rebuilt from it |
| Tests | Unity Test Framework (EditMode for simulation, PlayMode smoke tests) |  |
| Version control | Git + Git LFS | LFS for binary assets; ignore Library/ and Temp/ |

### Layers

1. **Simulation core** (`Hearthline.Sim`, plain C#, no UnityEngine references): characters, marks, lineage, household, settlements, factions, reputation, calendar, storyteller and event resolution. Deterministic, using a seeded RNG.
2. **Content** (`Content/`): YAML definitions for marks, traits, items, events, loot tables and factions. Loaded into simulation definitions at boot and validated in tests.
3. **Bridge** (`Hearthline.Bridge`): maps simulation state onto scenes. It spawns actors, applies mark visuals to character rigs, converts combat hits into simulation wound requests, and turns simulation events into staged scenes or storybook panels.
4. **Presentation** (`Hearthline.Game`): MonoBehaviours, controllers, cameras, animation, VFX, audio, UI.

Communication flows one way through **commands** (presentation asks the simulation to do something) and **domain events** (the simulation reports what happened). Presentation never mutates simulation state directly.

### Repository layout

```
/Assets
  /Hearthline
    /Sim            # pure C# simulation (asmdef with noEngineReferences)
    /Bridge         # sim <-> Unity mapping
    /Game           # MonoBehaviours, controllers, UI
    /Art /Audio /Prefabs /Scenes
  /ThirdParty       # purchased assets, untouched
/Content            # YAML: events/, marks/, items/, traits/, factions/
/Tests
  /EditMode         # simulation + content validation
  /PlayMode         # smoke tests
/docs
  DESIGN.md         # export of this document
  /adr              # architecture decision records
CLAUDE.md
```

### Performance budget (initial)

60 fps at 1440p on a mid-range GPU (RTX 3060 class), at most 40 active NPCs in a region, and save/load under 3 seconds.

## Working with Claude Code

The division of labor: Claude Code builds systems, tools, tests and content in bulk; Travis owns direction, asset selection, and everything judged by feel. Wire up the feedback loop (console, tests, Play Mode) before writing game code.

### Setup

1. Install Unity 6 and create a URP 3D project. Initialize git and Git LFS with a Unity .gitignore.
2. Install Unity's official Claude Code plugin (skills, Unity CLI and MCP editor control). Confirm Claude can read the console and run EditMode tests. Check whether any part requires a Unity AI subscription.
3. Put this document in the repo as `docs/DESIGN.md`, and write a short `CLAUDE.md` (below).
4. Create the folder layout and assembly definitions from the Technical architecture section, with `Hearthline.Sim` set to no engine references.

### CLAUDE.md essentials

- What the game is in 3 lines, plus a pointer to `docs/DESIGN.md`.
- Layer rules: the simulation core never references UnityEngine; presentation never mutates simulation state; everything that must persist lives in the simulation.
- Content rules: new events, marks and items go in `Content/` YAML and must pass the schema tests.
- Definition of done: compiles, EditMode tests pass, no new console errors, and the changed behavior is described in the commit message.
- Do not modify `/Assets/ThirdParty`.
- Commands: how to run tests through the Unity CLI and how to enter Play Mode through MCP.

### Working pattern

- **One system per session,** scoped by a short spec (a page at most): goal, data, rules, acceptance tests.
- **Simulation first, then presentation.** Build and test the logic headless, then wire it to the scene.
- **Content in bulk:** once a schema is stable, Claude drafts events in batches of 10 to 20. Travis edits for voice. Tests check references, reachability and outcome coverage.
- **Debug tools early:** a dev console and cheat panel (spawn event, add mark, skip season, kill head of household). These pay for themselves immediately.
- **Decision log:** record significant choices as short ADRs in `docs/adr/`, so later sessions don't relitigate them.

### What stays with Travis

Camera and combat feel tuning, animation selection and blending, lighting and post-processing, asset purchases, the writing voice, and judging whether a moment lands. Plan your own time around these; they are where the game becomes polished or doesn't.

## Scope and milestones

The first goal is a **vertical slice** that proves the core idea: one region, two generations, 3 to 4 hours of play, at near-final quality in a small area. Each milestone ends at a gate. Stop and reassess if a gate fails, rather than pushing on.

1. **M0 — Toolchain spike.** Unity project, Claude Code plugin with MCP, a purchased third-person controller, a greybox terrain, and one scar that persists through save and load.
   - Gate: the Claude Code loop (edit, compile, test, read console) feels fast, and adding a scar via the cheat panel shows on the model after reload.
2. **M1 — Body as record.** Full Mark data model, wounds from hit regions, infirmary treatment outcomes, 15 mark definitions, the visual layer (decals, materials, part swaps), and the mirror view.
   - Gate: a character carrying 6 or more marks reads as a person with a history in a single screenshot.
3. **M2 — Fast loop greybox.** Homestead slots (3 structures), a small greybox region, one hunt goal, carrying and hauling home, one ambush encounter, basic ranged combat, and household roles reporting results.
   - Gate: one out-and-back trip is fun in greybox.
4. **M3 — Event engine.** YAML schema and validator, storyteller pacing, casting, diegetic and storybook presentation, and 20 events covering every outcome type.
   - Gate: in 5 playthroughs of the same trip, the stories differ meaningfully.
5. **M4 — Slow loop.** Interludes, aging, partners, births, inheritance of traits and echoes, bequeathing gear, succession, and the family chronicle.
   - Gate: playing as the child of a character you played feels like a continuation, not a restart.
6. **M5 — Vertical slice.** Art pass on one region, Calvary Ford with 2 growth thresholds, 40 to 60 events, audio pass, stealth takedowns, 2 generations playable.
   - Gate: an outside player finishes it and tells a story about their family unprompted.

After M5, decide based on the slice: expand to the full game, cut scope, or rethink. Durations are not estimated here. Measure M0 and M1 first, then extrapolate.

## Decisions, open questions and risks

Several choices below are **defaults taken to keep the document concrete**. They are marked as such; change any of them before M0 and the affected sections need revising.

### Decisions

| Decision | Choice | Status |
| --- | --- | --- |
| Engine | Unity 6, URP, C# | Decided |
| Combat | Ranged first; melee only as stealth takedowns | Decided |
| Camera | Third-person, over the shoulder | Decided |
| Who you control | One head of household per generation | Default |
| Death | Permanent; heir succession; line ends = campaign ends | Default |
| Home scale | RDR2-camp scale growing to a 6–12 person hamlet; slot-based building | Default |
| Town | Calvary Ford grows through simulated values; no direct building | Default |
| Strangeness | Grounded near home, stronger with distance; never explained | Default |
| Region | Overgrown American Southeast | Default |
| Progression | No XP levels; skills by use, traits from events | Default |
| Content format | YAML in the repo with schema tests | Default |

### Open questions

- Can the player switch to other household members during a trip (Wildermyth-style party control), or only the head?
- Should a lineage be able to end early, or is there a soft fallback (adoption, a sworn successor)?
- How much on-screen romance and childbirth framing? A few dignified beats, or mostly off-screen during interludes?
- Final name and tone of the phenomenon ("the Neighbors" is a placeholder).
- Commercial intent: a personal project, or a possible Steam release? This affects asset licensing and how polished the slice must be.

### Risks

| Risk | Why it matters | Mitigation |
| --- | --- | --- |
| Animation and feel | The weakest area for an AI-assisted pipeline; the largest driver of perceived polish | Buy a strong controller and animation base; ranged-first; a dedicated feel pass |
| Content volume | Systemic storytelling needs hundreds of good events | YAML pipeline, bulk drafting by Claude, Travis edits for voice; reuse via casting |
| Mixed asset styles | Makes the game look cheap | One art family, shared materials and post-processing, decided in M0 |
| Scope creep | Five touchstone games each imply a large game | Pillars gate features; the M5 decision point is mandatory |
| Unity tooling churn | Its MCP and AI tools changed several times in 2026 | Keep the simulation engine-independent; pin package versions |
| Time-skip pacing | Interludes may feel like skipping the game | Prototype in M4 early; make interludes rich with events |

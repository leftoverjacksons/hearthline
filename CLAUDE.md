# Hearthline

Third-person homestead RPG in Unity 6 (URP, C#). You play one person, then their heirs, in an overgrown post-Unveiling American Southeast. Injuries, exposures and deeds leave permanent **Marks** on bodies, gear, the homestead and the family line.

The full design is in `docs/DESIGN.md`. Read the relevant section before working on a system. If a request conflicts with the design doc, say so before proceeding.

## Current milestone

**M0 — Toolchain spike.** (Update this line when the milestone changes.)
Gate: the edit → compile → test → console loop works through Claude Code, and a scar added from the cheat panel is still on the character model after save and load.

## Architecture rules (non-negotiable)

1. `Assets/Hearthline/Sim` is pure C#. Its asmdef has `noEngineReferences: true`. Never add `using UnityEngine` there.
2. Presentation (`Assets/Hearthline/Game`) never mutates simulation state directly. It sends **commands** to the sim and reacts to **domain events** from the sim.
3. `Assets/Hearthline/Bridge` is the only layer that knows about both the sim and Unity.
4. Anything that must persist lives in the simulation state. Scenes are rebuilt from it on load.
5. All randomness in the sim goes through the seeded `SimRandom`. No `System.Random` or `UnityEngine.Random` in the sim.
6. Never modify `Assets/ThirdParty/`. Wrap or extend purchased assets from `Game/` or `Bridge/`.

## Layout

```
Assets/Hearthline/Sim      pure C# simulation (marks, lineage, household, events, world state)
Assets/Hearthline/Bridge   sim <-> Unity mapping (spawning, mark visuals, hit -> wound requests)
Assets/Hearthline/Game     MonoBehaviours, controllers, cameras, UI
Assets/Hearthline/Tests/EditMode   sim + content validation tests (also run headless, see Commands)
Assets/Hearthline/Tests/PlayMode   smoke tests
Assets/Plugins             managed libraries the sim needs (YamlDotNet)
Assets/ThirdParty          purchased assets, untouched
Content/                   YAML: vocab.yaml, names.yaml, events/, marks/, items/, loot/, traits/, factions/, settlements/, structures/, jobs/
tools/headless             .NET projects that compile and test the sim without Unity
docs/DESIGN.md             design document
docs/content-schema.md     what content YAML may contain
docs/adr/                  architecture decision records
```

Tests live under `Assets/` because Unity only compiles there (ADR 0001). The design doc still shows the older `/Tests` layout.

## Content rules

- Events, marks, items, traits and factions are YAML in `Content/`, not C#.
- Every content file must pass the schema tests in `Assets/Hearthline/Tests/EditMode/ContentValidation`. The schema is described in `docs/content-schema.md`.
- Content may only reference stats, reaction tags, abilities, area tags, archetypes and encounters listed in `Content/vocab.yaml`. Add to the vocab first.
- Every event must change state and must cast the head of household; every beat needs one choice without requirements.
- Every Mark has a mechanical effect AND an origin text. No cosmetic-only marks.
- Event text uses casting tokens (`{hero.name}`, `{stranger.name}`) and references marks when relevant.

## Commands

Keep these current.

- Run EditMode tests without Unity (needs the .NET 8 SDK; this is what CI runs): `tools/test.sh`
- Validate content only: `tools/test.sh content`
Editor control goes through the Unity CLI (`winget install Unity.CLI`) and the `com.unity.pipeline` package; the Editor must be open on this project. `unity status` confirms the connection. The same commands are exposed as MCP tools by `unity mcp` (server `unity-editor-mcp`).

- Recompile and list compile errors: `unity recompile`
- Read the Editor console: `unity command console get`
- Run EditMode tests in the open Editor: `unity command test run --mode editor`
- Run PlayMode tests in the open Editor: `unity command test run --mode playmode --async-tests`, then `unity command test status --detail failures`
- Run tests with the Editor closed (batch mode): `unity test --mode EditMode`
- Enter / exit Play Mode: `unity command editor.playmode start` / `unity command editor.playmode stop`
- Discover other Editor commands: `unity command` (groups), `unity command <group>`

Unity's bundled NUnit is older than the headless NUnit 3.14: `Does.Not.Contain` only takes a string there. For collections use `Has.Member` / `Has.No.Member`.

The headless run and the Unity run compile the same test files. Write tests against NUnit 3 APIs that both have (`Assert.That`, `[Test]`, `[TestCase]`), and keep `UnityEngine` out of EditMode sim tests.

## Definition of done

A change is done when:
1. It compiles with no new warnings in `Hearthline.*` assemblies.
2. EditMode tests pass, and new sim logic has tests.
3. There are no new errors in the Unity console.
4. `docs/DESIGN.md` or an ADR is updated if the change alters a design decision.
5. The commit message says what changed and why.

## How to work in this repo

- One system per session. Start from a short spec: goal, data, rules, acceptance tests. If there is no spec, write one and ask for approval before coding.
- Simulation first, then presentation: build and test the logic headless, then wire it to scenes.
- Plan before changing more than 3 files, and list the files you expect to touch.
- Hand off narrow tasks to subagents in `.claude/agents/` (content writing, review, test triage) instead of doing everything in the main context.
- Record significant decisions as a short ADR in `docs/adr/NNNN-title.md`.
- Don't tune feel values (camera, recoil, animation blending, lighting) by guesswork. Expose them as serialized fields or ScriptableObjects and leave the tuning to Travis.

## Debug tools

Keep the dev console and cheat panel working. At minimum: spawn event by id, add or remove mark, skip season, set age, kill head of household, give item.

The cheat panel is `Assets/Hearthline/Game/CheatPanel.cs` (F1), with tabs for marks and gear, events and items, the household and homestead (structures, jobs, morale), and the world (settlements, townsfolk, grudges). When a system lands, add its cheats there.

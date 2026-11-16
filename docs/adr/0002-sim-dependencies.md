# 0002 — The sim depends on Newtonsoft.Json and YamlDotNet

Status: accepted (2026-11-16)

## Context

The sim owns saving (versioned JSON) and content loading (YAML). It may not reference UnityEngine, so
`JsonUtility` is out, and hand-written parsers are not worth maintaining.

## Decision

- JSON: Newtonsoft.Json. In Unity it comes from the package `com.unity.nuget.newtonsoft-json`; headless
  it comes from NuGet.
- YAML: YamlDotNet, committed as `Assets/Plugins/YamlDotNet/YamlDotNet.dll` (MIT). The headless build
  references that same file.
- `Hearthline.Sim.asmdef` lists both DLLs explicitly (`overrideReferences`).
- YAML is read through YamlDotNet's node model and mapped by hand (`YamlMap`), not through its
  reflection deserializer. That gives strict unknown-field errors and avoids reflection that IL2CPP
  could strip.

## Consequences

- Content lives in `Content/` beside `Assets/`, so it is not included in player builds. `SimHost` reads
  it from the repository in the Editor. A build step that copies it to `StreamingAssets/Content` is
  needed before the first player build. Not done yet.

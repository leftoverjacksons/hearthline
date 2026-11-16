# 0001 — Headless test harness; tests live under Assets/

Status: accepted (2026-11-16)

## Context

The simulation must be buildable and testable where there is no Unity Editor: cloud sessions and CI.
The design doc puts tests in `/Tests` at the repository root, but Unity only compiles scripts under
`Assets/` or inside packages.

## Decision

- EditMode and PlayMode tests live in `Assets/Hearthline/Tests/`.
- `tools/headless/` holds two .NET projects that compile the very same files: `Hearthline.Sim`
  (netstandard2.1, C# 9, warnings as errors — the constraints Unity applies) and `Hearthline.Tests`
  (NUnit 3). `tools/test.sh` runs them; GitHub Actions runs that on every push.
- EditMode tests for the sim use only NUnit 3 APIs present in both NUnit 3.14 and Unity Test Framework,
  and no `UnityEngine`.

## Consequences

- The sim's tests run in well under a second without the Editor.
- The headless run cannot catch Unity-specific problems (asmdef setup, IL2CPP stripping, `.meta` files).
  The Unity run is still required before a milestone gate.
- A referenced-packages-as-tests layout (`/Tests` as a local package) was rejected as more moving parts
  for no gain.

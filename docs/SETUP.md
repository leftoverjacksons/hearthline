# Setup: connecting a local Unity project to this repository

The repository was started without a Unity Editor. It contains the simulation, content, tests and a few
Unity scripts, but **no Unity project files** (`ProjectSettings/`, `Packages/`) and **no `.meta` files**.
Unity creates those; they then need to be committed. Nothing under `Assets/Hearthline/Bridge` or
`Assets/Hearthline/Game` has been compiled by Unity yet.

## First time

1. Install Git LFS once: `git lfs install`. Clone the repository.
2. In Unity Hub, create a new **Unity 6 (LTS)** project from the **Universal 3D (URP)** template in a
   temporary folder. Close the Editor.
3. Copy `ProjectSettings/` and `Packages/` from the temporary project into the clone, and merge the
   template's `Assets/` contents into the clone's `Assets/`.
4. Open the clone in Unity Hub (Add project from disk).
5. Package Manager → `+` → Install package by name: `com.unity.nuget.newtonsoft-json`. Also make sure
   **Test Framework** is installed.
6. Wait for compilation. Expected: no errors in `Hearthline.Sim`, `Hearthline.Bridge`, `Hearthline.Game`,
   `Hearthline.Tests.EditMode`. If `Hearthline.Sim` cannot find `Newtonsoft.Json.dll` or
   `YamlDotNet.dll`, check step 5 and that `Assets/Plugins/YamlDotNet/YamlDotNet.dll` imported.
7. Window → General → Test Runner → EditMode → Run All. The result should match `tools/test.sh`.
8. Commit everything Unity generated: `ProjectSettings/`, `Packages/`, and every new `.meta` file.

## Trying the M0 gate

1. In a scene, add an empty GameObject with **SimHost**, and another with **CheatPanel**.
2. Put **CharacterMarksView** on the character (or a capsule). Optionally assign a transform per body
   region; without anchors, marks appear at the object's origin.
3. Press Play. In the cheat panel (F1): add `scar_claw` to Face, Save, remove it or start a New game,
   then Load. The marker should be back.

That proves persistence through the sim with greybox markers. The gate itself asks for the scar on the
character **model**, which needs the purchased character and the decal layer.

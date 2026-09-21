# Equilibrium Existing Project Adoption

## Inventory

### Project identity and stage

- Unity project root: `D:/UnityProj/Equilibrium`.
- Editor version: Unity `6000.6.1f1`, from `ProjectSettings/ProjectVersion.txt`.
- Render stack: Universal Render Pipeline `17.6.0`, with the 2D Renderer as the default renderer and a second Universal Renderer used by the shaft bake camera.
- Evidence-based stage: early playable learning prototype. The project has an entry scene, placement interaction, prefabs, item-domain code, and a visual bake experiment, but no Unity test assemblies and no project-authored GDD or architecture document.
- CGS operating mode: `prototype` with `fast-prototype`, chosen to match the existing implementation and the personal learning scope. This label is descriptive rather than a strict production gate.

### Source code

- Runtime gameplay code is currently under `Assets/Script/`.
- Implemented domains:
  - grid placement and removal: `Assets/Script/MachinePlacer.cs`;
  - conveyor trigger motion: `Assets/Script/Conveyor.cs`;
  - item definitions and stacks: `Assets/Script/ItemDef.cs`, `Assets/Script/ItemStack.cs`;
  - bounded containers and transfers: `Assets/Script/IContainer.cs`, `Assets/Script/ItemContainer.cs`, `Assets/Script/ContainerTransfer.cs`;
  - producer/storage/transfer ownership: `Assets/Script/IItemContainerOwner.cs`, `Assets/Script/ItemSource.cs`, `Assets/Script/StorageBox.cs`, `Assets/Script/ItemTransferLink.cs`;
  - shaft frame animation: `Assets/Script/ShaftSpriteVisual.cs`.
- Editor tooling:
  - bake configuration component: `Assets/Art/Shaft/ShaftBaker.cs`;
  - custom Inspector and PNG sprite bake flow: `Assets/Editor/ShaftSpriteBakerEditor.cs`.
- No assembly definition files were found for project-authored runtime or editor code.

### Scenes and Prefabs

- Build entry: `Assets/Scenes/Main.unity`, enabled in `ProjectSettings/EditorBuildSettings.asset`.
- Main scene evidence:
  - `GameManager` contains `MachinePlacer`;
  - `machinePrefab` references `Assets/Script/Machine.prefab`;
  - `conveyorPrefab` is currently unassigned;
  - `ShaftTestA` contains `ShaftSpriteVisual` with 128 sprite frame references at 60 RPM;
  - `ShaftCamera` targets `Assets/Art/Shaft/ShaftRT.renderTexture` and selects renderer index 1;
  - `ShaftRender` contains `ShaftSpriteBaker`, configured for 128 frames at 256x256 output to `Assets/Art/Shaft/Baked`.
- Prefabs:
  - `Assets/Script/Machine.prefab` is a sprite-based one-cell machine visual;
  - `Assets/Script/Conveyor.prefab` has a trigger collider and `Conveyor` configured for speed 2 in the positive X direction.
- The former template `Assets/Scenes/SampleScene.unity` is deleted in the existing worktree. This adoption does not restore it.

### Assets

- `Assets/Art/GlassPipe/`: glass-pipe sprites and masks.
- `Assets/Art/Conveyor/`: conveyor source sprites.
- `Assets/Art/Shaft/`: OBJ/MTL/material/texture source, render texture, bake component, and 128 baked PNG frames.
- `Assets/Script/`: code, two prefabs, placeholder sprites, a grid shader, and its material. The folder mixes code and content and should remain unchanged during initial adoption.
- `Assets/Grid/`: currently an empty tracked Unity folder marker.
- `Assets/Settings/`: URP assets, renderer data, input actions, volume profile, and Unity 2D template content.

### Rendering and input

- `Assets/Settings/UniversalRP.asset` contains two renderer data entries:
  - index 0: `Assets/Settings/Renderer2D.asset`;
  - index 1: `Assets/Settings/Shaft_Renderer.asset`.
- All quality levels reference `Assets/Settings/UniversalRP.asset`.
- `ProjectSettings/GraphicsSettings.asset` has no global custom render pipeline assignment; the effective URP assignment is quality-level based.
- `Assets/Settings/Shaft.asset` is a separate URP asset for the shaft rendering experiment and is not the quality-level default.
- The Input System package is present. `MachinePlacer` reads keyboard and mouse directly through `UnityEngine.InputSystem`.

### Packages

- The project uses Unity 6 packages including URP 17.6.0, Input System 1.20.0, Test Framework 1.8.0, 2D tooling, AI Assistant/Inference, UGUI, Visual Scripting, and `com.unity.pipeline` 0.7.0-exp.1.
- Package state remains owned by `Packages/manifest.json` and `Packages/packages-lock.json`; adoption does not alter either file.

### Tests and documentation

- No Unity EditMode or PlayMode test source, test assembly, or `Assets/Tests/` directory was found.
- Root `tests/` contains CGS framework tests, not Equilibrium gameplay tests.
- Existing `docs/` content belongs to the CGS framework distribution. No evidence-backed Equilibrium GDD, architecture README, market overview, or production timeline existed before this adoption.

## Migration Map

| Existing project surface | CGS interpretation | Initial action |
| --- | --- | --- |
| `Assets/Script/` | Unity gameplay source plus colocated prefabs/materials | Keep in place; index as the current gameplay implementation. |
| `Assets/Editor/` | Unity editor-only tooling | Keep in place; treat as tooling ownership. |
| `Assets/Art/` | Source and generated visual assets | Keep in place; document source/generated boundary before any cleanup. |
| `Assets/Scenes/Main.unity` | Current playable/integration scene | Keep as the authoritative scene and build entry. |
| `Assets/Script/*.prefab` | Current placeable content | Keep references and GUIDs intact. |
| `Assets/Settings/` and `ProjectSettings/` | Active Unity render/input/build configuration | Keep unchanged; use as runtime truth. |
| `Packages/` | Unity dependency lock surface | Keep unchanged; review package necessity separately. |
| `.codex/studio.json` | CGS project identity and stage | Add minimal factual state for the existing project. |
| `production/adoption-report.md` | Adoption evidence and boundaries | Add this report. |
| `production/timeline.md` | Current milestone and next validation gate | Add a factual, lightweight timeline without inventing product commitments. |
| `design/gdd.md` | Missing project design reference | Propose later reverse-documentation from proven behavior; do not generate during inventory. |
| `docs/architecture/README.md` | Missing project architecture reference | Propose later after compile/play evidence; do not generate during inventory. |

## Do Not Move First

1. Do not move or rename `Assets/`, `Packages/`, or `ProjectSettings/`.
2. Do not reorganize `Assets/Script/` even though it mixes source and assets; prefab, scene, material, and sprite GUID relationships must be proven first.
3. Do not move `Assets/Scenes/Main.unity`, `Assets/Script/Machine.prefab`, or `Assets/Script/Conveyor.prefab`.
4. Do not separate `Assets/Art/Shaft/Baked/` from its baker until the generated/source boundary and regeneration procedure are verified in Unity.
5. Do not consolidate or delete `Assets/Settings/UniversalRP.asset`, `Assets/Settings/Shaft.asset`, `Assets/Settings/Renderer2D.asset`, or `Assets/Settings/Shaft_Renderer.asset` before confirming both camera paths.
6. Do not restore deleted Unity template content such as `Assets/Scenes/SampleScene.unity` or `Assets/Welcome/`.
7. Do not run the generic CGS `init` command against this root: its current implementation creates placeholder engine files under `src/` and writes seed documents that do not describe this project.
8. Do not revert or normalize the pre-existing dirty worktree as part of adoption.

## Adoption Risks

### Verified risks

- `Assets/Scenes/Main.unity` leaves `MachinePlacer.conveyorPrefab` unassigned, so switching to conveyor placement will reach the explicit `Prefab 没设置！` error path.
- There are no Unity gameplay tests despite container and transfer logic being suitable for deterministic EditMode tests.
- The project has no assembly definitions, so runtime/editor/test boundaries rely only on folder conventions and the default assemblies.
- Source and generated shaft frames share one art subtree without a checked-in generation manifest or provenance note.
- The worktree already contains Unity deletions, additions, and settings edits. Any broad migration would make attribution and rollback difficult.
- CGS framework surfaces are ignored by the current `.gitignore`, including `.codex/`, `.agents/`, `src/`, and `tests/`; newly added CGS state will remain local unless ignore policy is deliberately changed later.
- `unity status` found no connected Pipeline-enabled Editor instance, so live hierarchy, Console, compile, and Play Mode behavior were not verified during inventory.

### Unverified assumptions

- PC is recorded as the working platform because the project has Standalone scripting symbols and no contrary target evidence; the intended distribution target has not been documented.
- Automation/factory-building is an implementation inference from machines, conveyors, item production, transfer, and storage. It is not treated as a finalized genre or GDD claim.
- `Main.unity` is treated as authoritative because it is the only enabled build scene. Its complete gameplay loop has not yet been observed in Play Mode.

## Suggested Follow-up Documents

- After compile and Play Mode verification, reverse-document the observed interaction loop into a small `design/gdd.md` rather than inventing a full GDD.
- Add `docs/architecture/README.md` only after mapping runtime ownership, scene composition, ScriptableObject data, and editor-only bake tooling.
- Add a short shaft asset provenance/regeneration note before reorganizing art.
- Add a minimal Unity test plan and deterministic EditMode tests for `ItemStack`, `ItemContainer`, and `ContainerTransfer`.

## Changed Files

- Added `.codex/studio.json` as the minimal CGS project state. The current `.gitignore` keeps this file local.
- Added `production/adoption-report.md` as the evidence-backed adoption artifact.
- Added `production/timeline.md` as a lightweight current-milestone record.
- No file under `Assets/`, `Packages/`, or `ProjectSettings/` was changed by adoption.

## Verification Evidence

- `./codex-game-studio` was not executed. All CGS commands used the Windows launcher `./codex-game-studio.cmd` in PowerShell notation as `.\codex-game-studio.cmd`.
- `.\codex-game-studio.cmd context studio`: passed and resolved Equilibrium, Unity `6000.6.1f1`, `prototype`, and `fast-prototype`.
- `.\codex-game-studio.cmd status`: passed and reported the adoption phase as active.
- `.\codex-game-studio.cmd docs-impact --base HEAD`: passed; no functional game paths were changed by adoption.
- `.\codex-game-studio.cmd validate`: partial failure. The studio schema, role roster, workflow roster, Unity `ProjectSettings` marker, and this timeline are independently inspectable, but the stock validator assumes a generated Unity project below `src/` and requires seed documents and generated engine-reference copies. Those assumptions conflict with this root-level existing-project adoption and are intentionally not satisfied by moving or duplicating the Unity project.
- `unity status --project-path D:/UnityProj/Equilibrium --format json`: no connected Pipeline-enabled Editor instance was found. No compile, Console, Play Mode, or Unity Test Runner verdict is claimed.

## Next Owner or Decision

- Next owner: Unity specialist or gameplay programmer, using the existing root project in place.
- Next decision: open the existing project in Unity `6000.6.1f1`, obtain compile and Play Mode evidence, then decide whether to fix the missing conveyor prefab reference and add focused EditMode tests.
- Separate CGS tooling decision: adapt the validator to support a root-level existing Unity project, rather than creating `src/Packages` and `src/ProjectSettings` placeholders.

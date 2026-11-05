# Powered processing integration evidence

Date: 2026-11-05. Windows 11, Unity 6000.6.1f1. Workspace: `D:\UnityProj\Equilibrium`.
Owner: Simulation and runtime UI integration. Scope: `docs/architecture/machine-processing-runtime.md`.

## Changed surface

- New Simulation files in `Assets/Scripts/Simulation/World/`: MachineState, RecipeInputPlanner,
  ProcessingSystem, StraightMechanicalSolver, WorldWriter.Processing and their Unity-generated metas.
- Existing WorldWriter/WorldView/BlockDefinition: processing commit entry, persistent machine/fuel
  state, fuel engine and straight shaft definitions. Stable new payload and reason enum values.
- Tier1Content adds source/shaft numeric properties using existing ItemDef extras.
- FactorySession, FactoryAssets/FactoryRuntime, FactoryInputController, FactoryHud and
  FactoryWorldRenderer expose placement, material stock, inspection and output sliding visuals.
- FactoryHud.uss wraps the larger stock display. Inspector position follows toolbar geometry.
- New `Assets/Tests/EditMode/FactoryEditing/MachineProcessingTests.cs`; extended
  `Assets/Tests/PlayMode/Logistics/FactorySceneTests.cs`.

## Test evidence

- PureCheck after the creator's power-conservation correction: **168 passed, 3 existing Unity-only
  skips, 0 failed**, exit 0. `Logs/machine-pure-conservation-20261105.trx`.
- Full Unity EditMode after the correction: **182 passed, 1 existing skip, 0 failed**, exit 0.
  `Logs/machine-editmode-conservation-20261105.xml` and matching log.
- Full Unity PlayMode after the correction: **5 passed, 1 existing content-catalog skip, 0 failed**,
  exit 0, using `-nographics`. `Logs/machine-playmode-conservation-20261105.xml` and matching log.

The 24 new pure tests cover ten powered ticks at 135.5 W, zero fuel, strict overload boundary,
20-cell shaft efficiency/disconnection, ten-tick fractional fuel consumption, final fuel conservation,
atomic outputs/byproducts, retained byproduct roll, reservations, extraction protection, cancellation,
irreversibility, broken reservations, removal/replacement, admission, mixed/overlapping tags,
stable recipe selection/pinning, zero work, capped carry, 60-tick utilization and production delivery.

The new Main PlayMode test uses the actual placement controller, creates input/output chests, four
belt cells, two accessors, one saw, one fuel engine and a straight shaft. One log travels into the saw;
two planks reach the output chest. It also checks finite initial stock, deferred placement and the
machine inspection/renderer bindings. Existing logistics/UI regressions remain in the run.

The final runs include toolbar-dependent inspector position and material-action visibility changes.
No packaged Player or IL2CPP claim is made for this newly added implementation.

Graphical Main Play Mode verification after all source changes: the runtime-built fixture shows the
saw Working at 900 / 1350 J, receiving 300 W, with 2 completed batches and 4 planks in the output chest.
Its source load is 30.3 / 40 N*m. The stock preset was read from the existing FactoryPresentation
asset through Editor API and includes the new field defaults without reauthoring the asset.
The toolbar, machine actions and scrolling inspector render at 1101x506 without overlap with the
footer. Screenshot: `Logs/machine-hud-live-20261105.png`. The fixture exists only in Play Mode.

## Reproduction commands

Batch tests use process-local TEMP/TMP under `Logs/logistics-temp` and run after closing the clean Editor.

```powershell
dotnet test tools/purecheck/Equilibrium.PureCheck.csproj --no-restore --verbosity minimal --logger 'trx;LogFileName=machine-pure-conservation-20261105.trx' --results-directory Logs
unity test 'D:\UnityProj\Equilibrium' --editor-version 6000.6.1f1 --mode EditMode --report-format nunit --output 'D:\UnityProj\Equilibrium\Logs\machine-editmode-conservation-20261105.xml' --timeout 600 --format json --no-banner --no-pager --non-interactive -- -nographics -logFile 'D:\UnityProj\Equilibrium\Logs\machine-editmode-conservation-20261105.log'
unity test 'D:\UnityProj\Equilibrium' --editor-version 6000.6.1f1 --mode PlayMode --report-format nunit --output 'D:\UnityProj\Equilibrium\Logs\machine-playmode-conservation-20261105.xml' --timeout 600 --format json --no-banner --no-pager --non-interactive -- -nographics -logFile 'D:\UnityProj\Equilibrium\Logs\machine-playmode-conservation-20261105.log'
```

## Preservation and limits

Before and after the batch runs, Main SHA256 remains
`1AC34DC716141FC25DBB902E275A5898E3E2B60ADC439AA91015CB8F5619E7AA`, and the original FactoryPresentation
asset remains `7631E457C2D52F69874D10AF85A77D4EBBDD81B4922CFDDE8B521C4B7FD3828B`.
ProjectSettings SHA256 remains `709BA261FC6FA7F47A6FCA57CF1211A0149E58D8110BF4338C7FA984BAB5E078`.
No existing scene, prefab, package, artwork or shader is modified by this turn.
After graphical verification, Play Mode was stopped, Main reopened through Editor API, temporary
background execution/define changes restored, and all three hashes matched again. Test Runner left
no InitTestScene assets. Unity generated metas for every new runtime/test script.

Gears, complete multi-source networks, final state sprites/shaft animation, ledger events, save
capture/hydration and large-network performance acceptance are still open. Recipe-pin selection UI
is not implemented. Live-session persistent bags do not make the factory survive exiting Play Mode.
The creator explicitly approved the single-transmission-loss correction during this turn. The formula
is now received watts = torque * local speed; path efficiency increases source demand. The new energy
test checks 303.03 W at the source / 300 W at the saw through one 99% shaft, and 1980 J of total work
from one 8000 J wood item at 25% engine efficiency. Normative spec, GDD and formula registry agree.

## Studio checks

`codex-game-studio.cmd validate`: exit 1, the same eight pre-existing adoption/mapping failures
(approval/context stores, three src-based engine paths, three document entry points).
Output: `Logs/machine-cgs-validate-20261105.txt`.
`codex-game-studio.cmd docs-impact --base HEAD`: exit 0, reports no functional paths because of the
stale src mapping/untracked source. Output: `Logs/machine-docs-impact-20261105.txt`. This does not prove
coverage; the normative mechanics, mechanical GDD/formula registry, runtime docs, affected stories and
fresh active-session Documentation Impact record were explicitly updated.

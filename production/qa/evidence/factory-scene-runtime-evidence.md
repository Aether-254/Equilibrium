# Factory scene integration evidence

Date: 2026-11-05. Windows 11, Unity 6000.6.1f1. Workspace: `D:\UnityProj\Equilibrium`.
Owner: gameplay/UI integration. Scope and controls: `docs/architecture/factory-scene-runtime.md`.

## Implemented surface

- `Assets/Scripts/Editing/FactorySession.cs`, `StraightBeltPreview.cs`: finite local stock, build/remove
  intents, minimum-two-cell straight drag, obstacle clamp, pending construction, persistent chest recovery.
- `Assets/Scripts/Presentation/FactoryRuntime.cs`, `FactoryAssets.cs`, `FactoryWorldRenderer.cs`:
  explicit live session, original sprite references, pooled blocks/markers and committed-transfer interpolation.
- `Assets/Scripts/UI/FactoryHud.cs`, `FactoryInputController.cs`, `Assets/UI/FactoryHud.uss`,
  `FactoryTheme.tss`: toolbar, runtime inspection/configuration, UI hit/focus protection, camera controls.
- `Assets/Editor/FactorySceneSetup.cs`: explicit Editor API integration into existing Main. Runtime assets
  live in `Assets/Settings/FactoryPresentation.asset` and `FactoryPanelSettings.asset`.
- Supporting changes: edit payload/reason; edit-to-logistics dependency; Simulation-owned live entropy
  construction; ingress reset when a block is removed; UI asmdef Input System reference.
- Tests: `Assets/Tests/EditMode/FactoryEditing/FactorySessionTests.cs` and
  `Assets/Tests/PlayMode/Logistics/FactorySceneTests.cs`. PureCheck includes the new editing tests.

## Verification results

1. PureCheck: **144 passed, 3 Unity-only checks skipped, 0 failed**.
   `Logs/factory-pure-check-final-20261105.trx`.
   Tests cover delayed writes and finite stock, pending construction/cancellation, invalid path atomic
   rejection, chest persistence/new orientation, effective capacity, accessory refunds, stale targets,
   item conservation while removing a transporting cell, drag axis/reversal/obstacle behavior.
2. Unity scene installation: **exit 0**. `Logs/factory-setup-final-20261105.log` confirms the explicit
   scene root and original references. No new Unity project, replacement scene or moved asset was made.
3. Full Unity EditMode: **158 passed, 1 existing debug-only skip, 0 failed**; CLI exit 0.
   `Logs/factory-editmode-20261105.xml`. Includes the previously intermittent oversized-save case,
   all logistics regressions and Unity assembly-boundary checks (Editing does not own tie-break entropy).
4. Initial Main scene PlayMode subset: **2 passed, 0 failed**, CLI exit 0.
   `Logs/factory-scene-playmode-20261105.xml`. Verifies explicit entry/assets, disabled legacy placement,
   UI hit rejection, queued six-block construction, 32 wood delivered from chest to chest, actual slot
   counts and midpoint interpolation; release over UI cancels a drag.
5. Expanded full PlayMode: **4 passed, 1 existing content-catalog-generation skip, 0 failed** in NUnit
   XML (`Logs/factory-playmode-final-20261105.xml`). Adds text focus suppression and actual UITK Submit
   event -> queued Keep-20/wood configuration -> next-Tick commit. This run **did not exit cleanly**:
   after `Test run completed. Exiting with code 0`, Editor crashed with CLI exit -2147483645.
   The stack reaches `TMP_EditorResourceManager.cs:27` -> `AssetDatabase.FindAssets` during quitting.
   This is not a failing test assertion; the root cause of the shutdown crash is not established.
6. Real graphical Editor: opened Main, entered Play Mode, queued a two-chest/four-belt fixture and
   inspected Game View. Original belt art, count labels, Chinese toolbar and live inspection rendered.
   Screenshot: `Logs/factory-hud-live-20261105.png`. The fixture was runtime-only and was discarded.
   The final screenshot was captured again after increasing input contrast, splitting item/count labels
   onto two lines and keeping the scrolling inspector above the footer. Main remained clean on exit
   from Play Mode, and its saved SHA256 remained
   `1AC34DC716141FC25DBB902E275A5898E3E2B60ADC439AA91015CB8F5619E7AA`.
7. Final isolated full PlayMode rerun, adding `-nographics`: **4 passed, 1 existing skip, 0 failed**,
   CLI exit **0**. `Logs/factory-playmode-isolated-20261105.xml` and matching `.log`. This includes the
   expanded accessor Submit/focus checks and the final source changes. The generated test scene was
   removed by Test Runner; Main and ProjectSettings hashes matched the values below afterward.
   The earlier graphical shutdown crash did not reproduce in this run; its root cause remains unknown.

An earlier screenshot branch of the test used WaitForEndOfFrame in batch mode; that run failed only at
capture, after gameplay assertions. The test now captures only in an interactive Editor. An attempted
live Pipeline async rerun stalled behind a Bee build-system error; it was cancelled, then the batch
expanded run above produced results. These attempts are not counted as successful verification runs.

## Commands

Process-only `TEMP` and `TMP` use `D:\UnityProj\Equilibrium\Logs\logistics-temp` for batch validation.

```powershell
dotnet test tools/purecheck/Equilibrium.PureCheck.csproj --no-restore --verbosity minimal --logger 'trx;LogFileName=factory-pure-check-final-20261105.trx' --results-directory Logs
unity run 'D:\UnityProj\Equilibrium' --editor-version 6000.6.1f1 --timeout 600 --format json --no-banner --no-pager --non-interactive -- -nographics -executeMethod Equilibrium.Editor.FactorySceneSetup.InstallBatch -logFile 'D:\UnityProj\Equilibrium\Logs\factory-setup-final-20261105.log'
unity test 'D:\UnityProj\Equilibrium' --editor-version 6000.6.1f1 --mode EditMode --report-format nunit --output 'D:\UnityProj\Equilibrium\Logs\factory-editmode-20261105.xml' --timeout 600 --format json --no-banner --no-pager --non-interactive -- -nographics -logFile 'D:\UnityProj\Equilibrium\Logs\factory-editmode-20261105.log'
unity test 'D:\UnityProj\Equilibrium' --editor-version 6000.6.1f1 --mode PlayMode --report-format nunit --output 'D:\UnityProj\Equilibrium\Logs\factory-playmode-final-20261105.xml' --timeout 600 --format json --no-banner --no-pager --non-interactive -- -logFile 'D:\UnityProj\Equilibrium\Logs\factory-playmode-final-20261105.log'
unity test 'D:\UnityProj\Equilibrium' --editor-version 6000.6.1f1 --mode PlayMode --report-format nunit --output 'D:\UnityProj\Equilibrium\Logs\factory-playmode-isolated-20261105.xml' --timeout 600 --format json --no-banner --no-pager --non-interactive -- -nographics -logFile 'D:\UnityProj\Equilibrium\Logs\factory-playmode-isolated-20261105.log'
.\codex-game-studio.cmd validate
.\codex-game-studio.cmd docs-impact --base HEAD
```

## Scene preservation and cleanup

Before installation, Main SHA256 was
`C21354C86D8235B2BCAF7C875EBCFDA71EDFCAC36DC71F3F04B69DC958A92AB7`.
The comparison to that exact dirty-worktree scene, not Git HEAD, found zero removed YAML objects,
six added objects (factory root/transform/components), and only two changed existing records:
MachinePlacer enabled flag and scene root list. Evidence: `Logs/factory-scene-preservation-20261105.json`.
The pre-existing grid-position change against HEAD remains intact.

The abandoned Test Runner scene was deleted with AssetDatabase using its exact generated path.
PlayerSettings.runInBackground and Standalone defines were restored through Editor API to their
pre-session values. Verified ProjectSettings SHA256:
`709BA261FC6FA7F47A6FCA57CF1211A0149E58D8110BF4338C7FA984BAB5E078`.
Git Auto Sync was read as disabled. No commit, push, reset, package change or prototype deletion was requested.

## CGS and remaining acceptance

`validate` still returns the **8 pre-existing failures**: missing approval/context records, three old
`src/` Unity paths and three old document entry points. Final repeat:
`Logs/factory-cgs-validate-final-20261105.txt` (exit 1).
No template scaffolding was created merely to silence them.
`docs-impact --base HEAD` returns PASS/no-functional-paths because of the stale mapping and untracked
source; it does not establish documentation coverage. The owning runtime documents, partial story
statuses and active session's fresh Documentation Impact record were updated manually.
Final command output: `Logs/factory-docs-impact-final-20261105.txt` (exit 0).

No claim of finished production art, 4k-object performance, packaged Player behavior, full machine
inspection, save hydration, pathfinding/reuse/carry or powered processing. Leaving Play Mode currently
discards the factory; the existing MessagePack codec is not a live-world save feature yet.

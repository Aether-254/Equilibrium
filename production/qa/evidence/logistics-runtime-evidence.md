# Logistics runtime verification

Date: 2026-11-04

Status: logistics logic, Editor integration and runtime TickPump checks passed. Full EditMode has one
oversized-save memory failure; the entire game, world rendering and performance gate are not signed off.

## Changes and coverage

`LogisticsSystem`, `LogisticsSlots`, `LogisticsTypes` and `WorldWriter.Logistics` implement a complete
input chest / accessor / belt chain / accessor / output chest using the existing pure C# simulation.
`ITickBatchPlanner` adds a staging hook during Resolve; no inventory mutation occurs before Commit.
`BlockDefinition.Belt` exposes rear and side input ports with its existing output at local port index 1.

32 pure logistics cases cover whole groups and simultaneous vacancies, distinct item types, no partial
belt moves, side-input contention, no direct chest access, opposite outputs, partial accessor delivery,
Exact/UpTo/Keep, reservation safety, current-tick merge eligibility, accessor priority, filters, invalid
targets, ingress blocking, Auto flow with rotation, shared source/destination budgets, new-stock exclusion,
tidy and rollback, direct machine-port admission, 100 ticks of loop conservation, a 2,000-cell chain,
stale-batch atomic rejection, fully occupied cycle rejection, no Resolve mutation and attachment removal.

An Editor test creates the UI Toolkit workbench, blocks the real line, reads a stalled inspection result,
reopens it, transfers all 128 items, collects output and checks conservation. A PlayMode test drives
the same production logistics module from an actual `TickPump` MonoBehaviour across real Unity frames.

## Results

| Layer | Result | Artifact |
| --- | --- | --- |
| PureCheck | 136 total: 133 passed, 3 Unity-only skips, 0 failed | `Logs/logistics-pure-final.trx` |
| Unity EditMode logistics | 33 passed, 0 failed | Logistics cases in `Logs/logistics-editmode-20261104.xml` |
| Unity EditMode full run | 148 total: 146 passed, 1 failed, 1 skipped | `Logs/logistics-editmode-20261104.xml` |
| Unity PlayMode | 3 total: 2 passed, 1 skipped, 0 failed | `Logs/logistics-playmode-20261104.xml` |
| Isolated oversized-save rerun | 1 passed, 0 failed; 0.296 s | `Logs/logistics-save-oom-isolated-20261104.xml` |
| 2,000-cell cold whole tick | 1.382 ms in this Editor run | LongLineUsesIterativeDependencyResolution test output |
| Repository whitespace | `git diff --check` passed | Terminal output |
| CGS validation | Same 8 pre-existing template/project-layout failures | `Logs/cgs-validate-logistics-20261104.log` |

The PureCheck skips require Unity assembly boundaries. EditMode's skip is the pre-existing debug-only
snapshot-guard case. PlayMode's skip is the pre-existing Resources catalog test, because Tier 1 assets
have not been generated. The workbench uses the in-memory Tier 1 pack and does not depend on that asset.

## Failure follow-up

The first Unity compile exposed an NUnit API mismatch: Unity's `Does.Not.Contain` overload did not
accept the enum used by the pure .NET runner. The assertion was changed to an explicit `Any` check;
the next Unity compile succeeded and all logistics cases passed.

The full EditMode run failed only
`SaveLoad.SchemaV1Tests.HighlyCompressibleOversizeDocument_IsRefusedBeforeWritingAnUnreadableSave`.
`MessagePackWriter.FlushAndGetArray` threw `OutOfMemoryException` while producing the uncompressed
oversized document, rather than reaching `SaveTooLarge`. No save-codec source was changed in this task.
C: had approximately 0.03 GiB free during this run. This is an observed resource-risk condition, not
proof of the OOM's exact cause. A focused rerun is recorded separately and cannot retroactively turn
this full-suite result into a pass.

The focused rerun passed without a source change. The failure therefore did not reproduce in isolation;
full-run memory pressure remains a known verification limitation. No user applications or processes
were closed to obtain this result.

`docs-impact --base HEAD` returned PASS with "no functional game paths changed". The runtime source
tree is still untracked from earlier work, so that Git-based detector does not see the implementation
changes. The owning architecture, story and session documents were updated explicitly; the detector's
message is not evidence that this task made no functional change.

## Reproduction

From `D:\UnityProj\Equilibrium` in PowerShell:

```powershell
$env:TEMP = 'D:\UnityProj\Equilibrium\Logs\logistics-temp'
$env:TMP = $env:TEMP
[IO.Directory]::CreateDirectory($env:TEMP) | Out-Null
dotnet test tools/purecheck/Equilibrium.PureCheck.csproj --no-restore --verbosity minimal --logger 'trx;LogFileName=logistics-pure-final.trx' --results-directory Logs
unity test 'D:\UnityProj\Equilibrium' --editor-version 6000.6.1f1 --mode EditMode --report-format nunit --output 'D:\UnityProj\Equilibrium\Logs\logistics-editmode-20261104.xml' --timeout 600 --format json --no-banner --no-pager --non-interactive -- -nographics -logFile 'D:\UnityProj\Equilibrium\Logs\logistics-editmode-20261104.log'
unity test 'D:\UnityProj\Equilibrium' --editor-version 6000.6.1f1 --mode PlayMode --report-format nunit --output 'D:\UnityProj\Equilibrium\Logs\logistics-playmode-20261104.xml' --timeout 600 --format json --no-banner --no-pager --non-interactive -- -nographics -logFile 'D:\UnityProj\Equilibrium\Logs\logistics-playmode-20261104.log'
.\codex-game-studio.cmd validate
.\codex-game-studio.cmd docs-impact --base HEAD
```

Open `Equilibrium > Logistics > Open Workbench` for manual experimentation. No Play Mode is needed.
This workbench test verifies the UI tree and interaction with real simulation; no manual screenshot
review, standalone Player test or new IL2CPP build was performed for this feature.

## Preserved project state

Main scene and project settings matched their pre-task hashes after EditMode and PlayMode execution:

- `Assets/Scenes/Main.unity`: `C21354C86D8235B2BCAF7C875EBCFDA71EDFCAC36DC71F3F04B69DC958A92AB7`
- `ProjectSettings/ProjectSettings.asset`: `709BA261FC6FA7F47A6FCA57CF1211A0149E58D8110BF4338C7FA984BAB5E078`

No original asset, prefab, scene, prototype script, package or project-settings file was moved or
replaced. Editor generated metadata for the new code. Existing dirty worktree changes remain intact.
The new Editor assembly reference on EditMode tests enables the workbench integration test; it does
not add Editor references to runtime assemblies.

## Remaining boundaries

See `docs/architecture/logistics-runtime.md` for the system-batch event API, conservative handling of
concurrent structural writes, no same-tick retry of failed intake claims, full-cycle policy, and the
pending production rendering, settings intents, save hydration and machine-system integrations.

The cold 2,000-cell measurement does not establish the under-1-ms steady-state target. Conveyor and
accessor stories remain In Progress rather than being marked fully complete.

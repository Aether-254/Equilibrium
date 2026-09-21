# Story 001: Create assembly definitions and TickEngine skeleton

> **Epic**: Tick Simulation
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: M (3-4 h)
> **Manifest Version**: 2026-09-20.1
> **Last Updated**:

## Context

**GDD**: `design/gdd/tick-simulation.md`
**Requirement**: `TR-tick-001`, `TR-tick-004`

**Governing ADRs**: ADR-0007: Simulation/Presentation Split (primary), ADR-0001: Tick Transaction Engine
**ADR Decision Summary**: Eight assemblies with `Equilibrium.Simulation`, `.Content`, `.Editing` free of UnityEngine; `TickEngine` runs Snapshot/Collect/Validate/Resolve/Commit/Emit; `TickPump` MonoBehaviour advances whole ticks.

**Engine**: Unity 6000.6.1f1 | **Risk**: MEDIUM
**Engine Notes**: asmdef `noEngineReferences: true` for the three pure assemblies; use `System.Numerics` for math. Test asmdefs already exist under `Assets/Tests`.

**Control Manifest Rules (Foundation / Simulation)**:
- Required: code lives in its layer's assembly; simulation has no UnityEngine reference.
- Forbidden: editor code outside `Equilibrium.Editor`; UnityEngine in Simulation/Content/Editing.
- Guardrail: `Equilibrium.Tests.EditMode` references Simulation, Content, Editing only.

## Acceptance Criteria

- [ ] `Assets/Scripts/Simulation/Equilibrium.Simulation.asmdef`, `Assets/Scripts/Content/Equilibrium.Content.asmdef`, `Assets/Scripts/Content.Unity/Equilibrium.Content.Unity.asmdef`, `Assets/Scripts/Editing/Equilibrium.Editing.asmdef`, `Assets/Scripts/Presentation/Equilibrium.Presentation.asmdef`, `Assets/Scripts/UI/Equilibrium.UI.asmdef`, `Assets/Editor/Equilibrium.Editor.asmdef` exist with the dependency direction from architecture.md section 2.
- [ ] `Equilibrium.Simulation`, `.Content`, `.Editing` compile with `noEngineReferences: true`.
- [ ] `TickEngine` exposes `Advance()` that runs the six phases in order and increments `TickIndex`; `ITickSystem`, `Transaction`, `TickSnapshot`, `IWorldWriter`, `CommittedEvent`, `EventBus` types exist as in ADR-0001 Key Interfaces.
- [ ] `TickPump : MonoBehaviour` in Presentation advances N whole ticks per frame based on `TimeScale` and `Paused`; never a partial tick.
- [ ] Test asmdefs reference the new assemblies and the existing smoke tests still pass.

## Implementation Notes

From ADR-0007: create the folders under `Assets/Scripts/`; move `Assets/Editor/ShaftSpriteBakerEditor.cs` and `Assets/Art/Shaft/ShaftBaker.cs` into `Equilibrium.Editor` (baker component may need a runtime stub in Presentation if it is a MonoBehaviour on a scene object; if so, keep the MonoBehaviour in Presentation and the bake logic in Editor). From ADR-0001: phases are methods on `TickEngine`; systems register via `Register(ITickSystem)`; `EventBus` is a simple list of `CommittedEvent` per tick exposed as `ReadOnlySpan`. No pooling yet (story 002).

## Out of Scope

- Story 002: transaction buffers and pooling
- Story 003: validation and dependency resolution
- Story 004: tie-break source

## QA Test Cases

- **AC-1/2**: assemblies compile
  - Given: the project opens in Unity
  - When: compilation finishes
  - Then: zero errors; reflection test asserts `typeof(TickEngine).Assembly` has no reference to `UnityEngine.CoreModule`
  - Edge cases: `Equilibrium.Content.Unity` may reference UnityEngine
- **AC-3**: phase order
  - Given: a `TickEngine` with a recording fake `ITickSystem`
  - When: `Advance()` once
  - Then: recorded phase sequence equals [Snapshot, Collect, Validate, Resolve, Commit, Emit]; `TickIndex == 1`
- **AC-4**: whole ticks
  - Given: `TickPump` with `TimeScale = 2.5`
  - When: 1 s of simulated frames elapse
  - Then: exactly 2 ticks advanced, 0.5 carried; with `Paused = true` zero ticks advance

## Test Evidence

**Story Type**: Integration
**Required evidence**: `Assets/Tests/EditMode/TickSimulation/TickEngineSkeletonTests.cs` passes.
**Status**: [ ] Not yet created

## Dependencies

- Depends on: None
- Unlocks: every other story in every epic

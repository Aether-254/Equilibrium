# Epic: Tick Simulation

> **Layer**: Foundation
> **GDD**: design/gdd/tick-simulation.md
> **Architecture Module**: TickEngine, TieBreakSource, EventBus (Equilibrium.Simulation)
> **Status**: Ready
> **Priority**: MVP
> **Engine Risk**: LOW (pure C#)
> **Manifest Version**: 2026-09-20.1
> **Stories**: Not yet created — run `/create-stories tick-simulation`

## Overview

Implement the transaction engine that owns the tick lifecycle: snapshot the world, collect transactions from registered `ITickSystem`s, validate, resolve by priority/dependency/non-reproducible tie-break, commit atomically through `IWorldWriter`, and emit `CommittedEvent`s. Includes the per-tick CSPRNG-seeded `TieBreakSource`, pooled transaction buffers, cycle detection, snapshot-violation checks in debug builds, and the `TickPump` MonoBehaviour that advances whole ticks. This is the first assembly to exist (`Equilibrium.Simulation`) and every other epic writes against it.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0001: Tick Transaction Engine | Pure C# six-phase engine; IWorldWriter sole mutator; pooled struct transactions | LOW |
| ADR-0002: Tie-Break Entropy Source | OS CSPRNG per tick -> SplitMix64 -> Xoshiro256**; never persisted | LOW |
| ADR-0007: Simulation/Presentation Split | Assembly layout; TickPump in Presentation | MEDIUM |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-tick-001 | 1 tick = 1 s; all state changes are transactions against a tick-start snapshot | ADR-0001 |
| TR-tick-002 | Atomic commit; dependency failure cascades; loser payload stays at source | ADR-0001 |
| TR-tick-003 | Equal-priority conflicts ordered by non-reproducible per-tick entropy | ADR-0002 |
| TR-tick-004 | Visual and ledger systems consume emitted events only | ADR-0001, ADR-0007 |
| TR-tick-005 | 10k conveyor transactions under 4 ms; zero steady-state allocation | ADR-0001 |

## Definition of Done

- All stories implemented, reviewed, closed via `/story-done`.
- All acceptance criteria in `design/gdd/tick-simulation.md` verified as EditMode tests in `Assets/Tests/EditMode/`.
- Benchmark test for TR-tick-005 recorded.
- Mutation audit test proves no writes outside `IWorldWriter`.
- `Equilibrium.Simulation.asmdef` exists with no UnityEngine reference.

## Next Step

Run `/create-stories tick-simulation`.

# Story 002: Transaction buffers, snapshot, and atomic commit

> **Epic**: Tick Simulation
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-09-20.1
> **Last Updated**:

## Context

**GDD**: `design/gdd/tick-simulation.md`
**Requirement**: `TR-tick-001`, `TR-tick-002`, `TR-tick-005`

**Governing ADRs**: ADR-0001: Tick Transaction Engine
**ADR Decision Summary**: Transactions are pooled structs with source, target, payload, priority, dependencies, validator; commit is all-or-nothing via `IWorldWriter`; snapshot is a read view, not a copy.

**Engine**: Unity 6000.6.1f1 | **Risk**: LOW

**Control Manifest Rules (Simulation)**:
- Required: Collect reads only `TickSnapshot`; steady-state tick is allocation-free; one `CommittedEvent` per commit.
- Forbidden: writes outside `IWorldWriter.Apply`; reads of live tables during Collect.
- Guardrail: 10k transactions under 4 ms.

## Acceptance Criteria

- [ ] `TransactionBuffer` stores `Transaction` structs in a pooled array; `Add` does not allocate after warmup (1000 adds, `GC.GetAllocatedBytesForCurrentThread` delta 0).
- [ ] `TickSnapshot` is a readonly struct of views; mutating the world after snapshot does not change values read through the snapshot within the same tick.
- [ ] A transaction whose validator fails produces zero change in source and target (GDD AC 2).
- [ ] A transaction with source and target commits both sides in one `IWorldWriter.Apply`; a fault injected between deduction and write leaves state unchanged (all-or-nothing).
- [ ] Debug build: a system reading live state during Collect fails its transaction with `SnapshotViolation`.
- [ ] Benchmark: 10,000 trivial move transactions across a fake world complete in under 4 ms on the reference PC.

## Implementation Notes

Use a minimal `FakeWorld` with two integer "slots" for this story; the real tables arrive in grid-world/container. `IWorldWriter.Apply` must stage then swap (write to a scratch record, then assign) so a validator exception cannot half-apply. Snapshot violation detection: a `[Conditional("DEBUG")]` guard flag on the world set during Collect; live accessors throw when the flag is set.

## Out of Scope

- Story 003: priority, dependency, conflict resolution
- Story 004: tie-break entropy

## QA Test Cases

- **AC-1**: pooled buffer
  - Given: warmed `TransactionBuffer`
  - When: 1000 `Add`
  - Then: allocated bytes delta == 0
- **AC-2**: snapshot isolation
  - Given: snapshot taken with slot A = 5
  - When: world A set to 9 outside commit
  - Then: `snapshot.A == 5`
- **AC-3**: failed validation
  - Given: transfer 10 from A(5) to B(0), validator requires source >= 10
  - When: tick runs
  - Then: A == 5, B == 0, one rejected event with reason
- **AC-4**: atomicity
  - Given: writer that throws after deducting
  - When: commit
  - Then: A and B unchanged; engine surfaces the fault
- **AC-6**: benchmark
  - Given: 10k transactions
  - When: `Advance()`
  - Then: elapsed < 4 ms (advisory in CI)

## Test Evidence

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/TickSimulation/TransactionCommitTests.cs`
**Status**: [ ] Not yet created

## Dependencies

- Depends on: Story 001
- Unlocks: Story 003

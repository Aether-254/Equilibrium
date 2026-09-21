# Story 003: Priority, dependency ordering, and conflict resolution

> **Epic**: Tick Simulation
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-09-20.1
> **Last Updated**:

## Context

**GDD**: `design/gdd/tick-simulation.md`
**Requirement**: `TR-tick-002`, `TR-tick-003`

**Governing ADRs**: ADR-0001: Tick Transaction Engine (primary), ADR-0002: Tie-Break Entropy Source
**ADR Decision Summary**: Resolve = topological pass over declared dependencies, then conflict groups sorted by `(-priority, tieBreakKey)`; cycles reject with `TransactionCycle`.

**Engine**: Unity 6000.6.1f1 | **Risk**: LOW

**Control Manifest Rules (Simulation)**:
- Required: explicit `Priority` (default 0); dependencies declared when correctness needs ordering; tie-break only via `ITieBreakSource.NextKey()` inside Resolve.
- Forbidden: predictable tie-break seeds.
- Guardrail: cycle detection must reject, never hang.

## Acceptance Criteria

- [ ] Two priority-0 transactions targeting the same single-capacity slot: exactly one commits, the other stays at source (GDD AC 3).
- [ ] Priority 5 vs priority 0 in conflict: priority 5 commits (GDD AC 5).
- [ ] Chain A -> B -> C with A=100, B=200, C=empty and B's move declared as A's dependency: after one tick A=empty, B=100, C=200 (GDD AC 6).
- [ ] Dependency cycle X depends on Y depends on X: both rejected with `TransactionCycle`; tick completes.
- [ ] A dependency that fails validation causes its dependents to fail with `DependencyFailed`.
- [ ] Conflict groups with > 1000 dependencies emit a performance warning event and still complete.

## Implementation Notes

Build the dependency graph from `DependsOn` lists; Kahn's algorithm with a visited set sized to the buffer; detect cycles as leftover nodes. Conflict detection: group candidates by contested resource key (source id + target id + slot) while accumulating remaining capacity from the snapshot; within a group sort by `(-Priority, key)` where `key` comes from an injected `ITieBreakSource` (this story uses `FixedTieBreakSource` in tests; real source in story 004). Loser transactions are marked rejected with `Contended`.

## Out of Scope

- Story 004: real CSPRNG tie-break source

## QA Test Cases

- **AC-1**: equal priority
  - Given: A and C both move into empty B (capacity 1 group)
  - When: tick with fixed tie-break [C, A]
  - Then: B holds C's group; A unchanged; A's transaction reason `Contended`
- **AC-2**: priority
  - Given: priorities 5 and 0
  - When: tick
  - Then: 5 commits regardless of tie-break key
- **AC-3**: chain
  - Given: A=100, B=200, C empty; A depends on B
  - When: tick
  - Then: A empty, B 100, C 200; groups are distinct objects
- **AC-4**: cycle
  - Given: X depends on Y, Y depends on X
  - When: tick
  - Then: both rejected `TransactionCycle`; `Advance()` returns
- **AC-5**: cascade
  - Given: dependent D on failing F
  - When: tick
  - Then: D rejected `DependencyFailed`

## Test Evidence

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/TickSimulation/ResolveTests.cs`
**Status**: [ ] Not yet created

## Dependencies

- Depends on: Story 002
- Unlocks: Story 004; conveyor epic

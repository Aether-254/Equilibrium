# Story 004: CSPRNG tie-break source

> **Epic**: Tick Simulation
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: S
> **Manifest Version**: 2026-09-20.1
> **Last Updated**:

## Context

**GDD**: `design/gdd/tick-simulation.md`
**Requirement**: `TR-tick-003`

**Governing ADRs**: ADR-0002: Tie-Break Entropy Source
**ADR Decision Summary**: Each tick: 32 bytes from `RandomNumberGenerator.Fill`, mixed with tickIndex via SplitMix64 finalizer, seeding a Xoshiro256** stream for that tick's keys. CSPRNG failure retries once then halts. Never persisted.

**Engine**: Unity 6000.6.1f1 | **Risk**: LOW
**Engine Notes**: verify `RandomNumberGenerator.Fill(Span<byte>)` availability on the project's .NET profile and IL2CPP.

**Control Manifest Rules (Simulation)**:
- Required: tie-break only inside Resolve.
- Forbidden: `UnityEngine.Random`, time seeds, world-state hashes, persisting RNG state; use from Editing assembly.
- Guardrail: CSPRNG failure halts with visible error.

## Acceptance Criteria

- [ ] `TieBreakSource.BeginTick(t)` draws fresh OS entropy; `NextKey()` returns 64-bit keys from Xoshiro256** seeded per ADR formula.
- [ ] Two engine runs from an identical save produce different key sequences over 1000 draws (assert not equal) (GDD AC 4).
- [ ] 10,000 two-way contentions resolved by the source split within 48-52% (statistical).
- [ ] Injected CSPRNG failure: one retry, then `TieBreakEntropyUnavailable` thrown and the tick does not commit.
- [ ] `FixedTieBreakSource` exists only in `Equilibrium.Tests.EditMode`; a reflection test asserts no type implementing `ITieBreakSource` other than `TieBreakSource` exists in production assemblies.
- [ ] Static check: `Equilibrium.Editing` has no reference to `ITieBreakSource`.

## Implementation Notes

Wrap `RandomNumberGenerator` behind an internal `IEntropyProvider` so tests can inject failure. SplitMix64 finalizer: `z ^= z >> 30; z *= 0xBF58476D1CE4E5B9; z ^= z >> 27; z *= 0x94D049BB133111EB; z ^= z >> 31`. Expand the 64-bit seed to four Xoshiro state words with SplitMix64 steps. Byproduct probability rolls (machine-processing) will call `NextKey()` at completion.

## Out of Scope

- Consumers (conveyor contention, byproducts) in their own epics

## QA Test Cases

- **AC-2**: non-reproducibility
  - Given: two `TieBreakSource` instances, `BeginTick(1)` each
  - When: 1000 `NextKey()` each
  - Then: sequences differ
- **AC-3**: fairness
  - Given: 10k conflicts between two equal candidates
  - When: resolved
  - Then: winner counts each in [4800, 5200]
- **AC-4**: failure
  - Given: entropy provider throws twice
  - When: `BeginTick`
  - Then: `TieBreakEntropyUnavailable`; engine reports halt
- **AC-6**: assembly boundary
  - Given: compiled assemblies
  - When: reflect `Equilibrium.Editing` referenced types
  - Then: `ITieBreakSource` absent

## Test Evidence

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/TickSimulation/TieBreakSourceTests.cs`
**Status**: [ ] Not yet created

## Dependencies

- Depends on: Story 003
- Unlocks: Story 005

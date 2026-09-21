# ADR-0002: Tie-Break Entropy Source

## Status
Accepted

## Date
2026-09-20

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6000.6.1f1 |
| **Domain** | Core |
| **Knowledge Risk** | LOW for this ADR: uses .NET `System.Security.Cryptography.RandomNumberGenerator`, stable across Unity 6 .NET Standard 2.1 / .NET 8 profiles |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `docs/engine-reference/unity/current-best-practices.md` |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | `RandomNumberGenerator.Fill` availability and cost on Windows IL2CPP and Mono builds |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-0001 |
| **Enables** | Conveyor contention, machine byproduct rolls, market same-priority settlement |
| **Blocks** | conveyor epic (contention tests) |
| **Ordering Note** | Implement alongside ADR-0001 resolver |

## Context

### Problem Statement
Spec s1.3 and the tick-simulation GDD require that equal-priority transaction conflicts be decided by a random order that players cannot reproduce, predict, or manipulate from world seed, save state, tick index, coordinates, or transaction IDs. The design goal is to make RNG manipulation of contention impossible. Pathfinding tie-breaks, by contrast, must be deterministic (placement-editing GDD rule 20), so the two must not share a source.

### Constraints
- Must cost well under 0.1 ms per tick.
- Must work headless and in offline recompute.
- Must never fall back to a predictable seed.

### Requirements
- Per-tick fresh entropy from the OS CSPRNG mixed into a fast PRNG for that tick's draws.
- Not persisted in saves.
- Failure of the CSPRNG halts the tick rather than degrading.

## Decision

`TieBreakSource : ITieBreakSource` (in `Equilibrium.Simulation`) does, at the start of each tick's Resolve phase:

1. `RandomNumberGenerator.Fill(span32)` to obtain 32 bytes of OS entropy.
2. `seed = SplitMix64Finalizer(bytes[0..8] ^ bytes[8..16] ^ bytes[16..24] ^ bytes[24..32] ^ tickIndex)`.
3. Instantiate a `Xoshiro256**` state from `seed` expanded by SplitMix64; all `NextKey()` calls in this tick draw from it.

Decision (user, 2026-09-20): per-tick mixing, not a session-level seed. Byproduct probability rolls (machine-processing) also use this source. Pathfinding and any preview logic use a separate deterministic comparator and never call `ITieBreakSource`.

### Architecture Diagram
```
OS CSPRNG --32 B--> [mix with tickIndex] --> Xoshiro256** (per tick) --> NextKey() --> Resolver sort key
```

### Key Interfaces
```csharp
public interface ITieBreakSource { void BeginTick(ulong tickIndex); ulong NextKey(); }
public sealed class TieBreakSource : ITieBreakSource { /* as above; throws TieBreakEntropyUnavailable on CSPRNG failure after one retry */ }
```
Test double `FixedTieBreakSource` exists only in test assemblies for deterministic unit tests of other systems; production code cannot reference it (assembly boundary).

## Alternatives Considered

### Alternative 1: Session-level CSPRNG seed
- **Description**: Draw once at session start; deterministic within session.
- **Pros**: Easier debugging; one syscall.
- **Cons**: Within a session an observer who reconstructs the state can predict outcomes; weaker than spec intent.
- **Rejection Reason**: User chose per-tick mixing.

### Alternative 2: `UnityEngine.Random` / `System.Random` seeded by time
- **Description**: Standard PRNG seeded by wall clock.
- **Pros**: Trivial.
- **Cons**: Predictable; time-derived seeds are exactly what spec forbids.
- **Rejection Reason**: Violates s1.3.

### Alternative 3: Hash of world state
- **Description**: Derive from save state hash.
- **Pros**: Reproducible for debugging.
- **Cons**: Reproducible for players too.
- **Rejection Reason**: Explicitly forbidden by spec.

## Consequences

### Positive
- Contention outcomes cannot be farmed.
- Offline recompute is statistically equivalent, not bit-identical, which the offline-progression GDD accepts.

### Negative
- Bug reproduction of contention-dependent issues requires the `FixedTieBreakSource` test double; production replays are not bit-exact.

### Risks
- CSPRNG unavailable on some platform: halt with visible error (GDD edge case); PC-only target makes this remote.
- Developers use the source for non-contention randomness: forbidden pattern registered; code review.

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| tick-simulation.md | Conflict rules 3-5; seed formula; CSPRNG failure edge case | `TieBreakSource` implementation |
| conveyor.md | Rule 8 contention | Resolver uses `NextKey()` |
| machine-processing.md | Probabilistic byproduct edge case | Same source at completion |
| placement-editing.md | Rule 20 deterministic preview tie-break | Explicitly separate comparator; forbidden to use this source |
| offline-progression.md | Rule 3 fresh entropy offline | Same class used in recompute |

## Performance Implications
- **CPU**: One 32-byte CSPRNG call per tick (microseconds) plus xoshiro draws.
- **Memory**: 32 bytes state.
- **Load Time**: None.
- **Network**: None.

## Migration Plan
None; new code.

## Validation Criteria
- Statistical test: 10k two-way contentions approximate 50/50.
- Two runs from the same save produce differing contention sequences (assert not equal over 1000 draws).
- Static check: no reference to `ITieBreakSource` from `Equilibrium.Editing` (pathfinding) assembly.

## Related Decisions
- ADR-0001
- design/gdd/tick-simulation.md

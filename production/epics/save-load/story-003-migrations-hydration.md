# Story 003: Migrations, validation refusals, and Unknown hydration

> **Epic**: Save / Load
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-09-20.1
> **Last Updated**:

## Context

**GDD**: `design/gdd/save-load.md`
**Requirement**: `TR-save-002`, `TR-save-003`

**Governing ADRs**: ADR-0005 (primary), ADR-0003, ADR-0004
**ADR Decision Summary**: ordered `ISaveMigration{From,To,Apply}`; refuse newer schema and unsupported world size; corrupt -> offer backups; `UnknownItem`/`UnknownBlock` hydration.

**Engine**: Unity 6000.6.1f1 | **Risk**: LOW

**Control Manifest Rules (Persistence)**:
- Required: every schema change adds a migration + fixture; fixture corpus loads in CI.

## Acceptance Criteria

- [ ] Save v1 loaded into a build at v3 applies M_1 then M_2 in order and validates (GDD AC 5).
- [ ] Newer schema refused with `SaveFromNewerVersion`; file unchanged (GDD AC 6).
- [ ] Truncated file -> `SaveCorrupt`; UI offers the most recent valid backup (GDD AC 7).
- [ ] Missing Addon namespace -> `UnknownItem` in the same slot with id and count; re-enabling the Addon restores (GDD AC 2).
- [ ] Missing block def -> `UnknownBlock` occupying its cells, holding persistent attributes, connectable to nothing, removable into an `UnknownItem`.
- [ ] Missing `slotKey` -> `PortBindingMissing`; missing orientation -> 0 with warning; world larger than supported -> `WorldSizeUnsupported`.
- [ ] Fixture corpus under `Assets/Tests/EditMode/SaveLoad/Fixtures/` loads across all migrations.

## Implementation Notes

Migrations operate on the generic `SaveDocument` map before typed hydration. Provide `M_1to2` as a no-op sample to exercise the chain in tests. `UnknownBlock` is a synthetic block def with zero ports.

## Out of Scope

- Offline elapsed time semantics (offline-progression)

## QA Test Cases

- **AC-1**: v1 fixture -> loads at v3 with migration log [1->2, 2->3]
- **AC-2**: v99 fixture -> refused
- **AC-3**: truncated fixture -> `SaveCorrupt`; backups listed
- **AC-4/5**: unknown namespace / unknown block hydration
- **AC-7**: all fixtures load

## Test Evidence

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/SaveLoad/MigrationAndHydrationTests.cs`
**Status**: [ ] Not yet created

## Dependencies

- Depends on: Story 002
- Unlocks: market-interface 003 (binding persistence), multiblock, offline-progression

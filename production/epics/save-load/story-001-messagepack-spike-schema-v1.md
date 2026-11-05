# Story 001: MessagePack compatibility spike and SaveDocument schema v1

> **Epic**: Save / Load
> **Status**: Complete
> **Layer**: Core
> **Type**: Integration
> **Estimate**: M
> **Manifest Version**: 2026-09-20.1
> **Last Updated**: 2026-11-04

## Context

**GDD**: `design/gdd/save-load.md`
**Requirement**: `TR-save-002`, `TR-save-003`

**Governing ADRs**: ADR-0005: Save Format (primary), ADR-0003
**ADR Decision Summary**: MessagePack-CSharp with AOT resolvers + LZ4; string keys; `Extras` bag on every record; JSON+gzip fallback behind the same `ISaveable` interfaces if AOT fails.

**Engine**: Unity 6000.6.1f1 | **Risk**: MEDIUM
**Engine Notes**: MessagePack-CSharp 3.1.12 with statically registered custom formatters passed Windows IL2CPP on Unity 6000.6.1f1. This path uses checked-in formatter code for raw unknown-member preservation, not runtime generation or resolver discovery. Allowed Libraries and ADR-0005 record the implementation.

**Control Manifest Rules (Persistence)**:
- Required: string keys; `Extras`; versioned sections.
- Forbidden: indices; Tag expansions; RNG state.

## Acceptance Criteria

- [x] MessagePack-CSharp pinned in `Packages/manifest.json`; an IL2CPP Windows build serializes and deserializes a test record with AOT resolvers (or the JSON fallback decision is recorded and implemented instead).
- [x] `SaveDocument` schema v1: `header{schemaVersion, gameVersion, addons[], marketBinding, lastSaveWallTime, tickIndex}`, sections `world`, `containers`, `mech`, `market`, `ledger`, `unlocks`, `tasks`, `extras`.
- [x] Every record type has `IDictionary<string, object> Extras`; unknown keys in input survive a round-trip byte-for-byte in value.
- [x] `ItemId` serializes as canonical string; a reserved Tag-matched input serializes as the concrete ItemId (GDD AC 3).
- [x] No RNG or time-control fields exist in any section (reflection test over record types).
- [x] Debug menu `Equilibrium/Save/Export JSON` writes a readable JSON of the current published/inspected document. Live factory publication is connected by story 002; this menu is disabled before a document exists.

## Implementation Notes

Custom `IMessagePackFormatter` for records captures unknown map keys into `Extras` and writes them back with the original location and encoded value. A closed resolver, `UntrustedData` bounds and `Lz4BlockArray` are configured explicitly. Keep future `ISaveable` implementations in Simulation; file IO in Presentation.

## Out of Scope

- Story 002: SaveService, boundary, backups
- Story 003: migrations and hydration

## QA Test Cases

- **AC-1**: build and run round-trip on IL2CPP (manual + PlayMode)
- **AC-3**: input with extra key `"future": 42` -> output contains it
- **AC-4**: reserved input from `#ingot` -> string `equilibrium:iron_ingot`
- **AC-5**: reflection: no field named like `rng`, `seed`, `timeScale` in save records

## Test Evidence

**Story Type**: Integration
**Required evidence**: `Assets/Tests/EditMode/SaveLoad/SchemaV1Tests.cs`; IL2CPP result note in `production/qa/evidence/save-il2cpp-spike-evidence.md`
**Status**: [x] Created and passed — see `production/qa/evidence/save-il2cpp-spike-evidence.md`. Final Unity EditMode: 114 passed, 1 pre-existing debug-define skip, 0 failed; PureCheck: 101 passed, 3 Unity-only skips, 0 failed. Windows IL2CPP build and Player probe both exited 0; Player wrote its PASS marker and actual EQS/JSON artifacts.

## Implementation outcome

- `Assets/Scripts/Simulation/Save/`: versioned DTOs, explicit AOT-compatible formatters, closed resolver, LZ4 codec and immutable raw unknown values.
- `Assets/Scripts/Presentation/SaveDocumentSession.cs`: detached current-document inspection snapshot.
- `Assets/Editor/SaveTools.cs`: inspect existing EQS and Export JSON menus.
- `Assets/Editor/SaveSpikeBuild.cs`, `Assets/Scripts/Presentation/SaveCodecPlayerProbe.cs`: batch-only build and compile-guarded Player round-trip probe.
- `Assets/Tests/EditMode/SaveLoad/`: codec, schema, raw-value, reservation, corruption and debug-session coverage.
- `tools/purecheck/Equilibrium.PureCheck.csproj`: pinned MessagePack verification dependency; `tools/repair-save-package-imports.ps1`: repair prior raw NuGet extraction import settings without relocating files.
- Format details and implementation clarification: `docs/architecture/save-format-v1.md`, ADR-0005. Explicit checked-in formatters replace reliance on automatic formatter generation because unknown values must remain byte-exact.

## Dependencies

- Depends on: item-registry 003
- Unlocks: Story 002

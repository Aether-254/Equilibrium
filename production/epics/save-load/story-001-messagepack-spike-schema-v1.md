# Story 001: MessagePack compatibility spike and SaveDocument schema v1

> **Epic**: Save / Load
> **Status**: Ready
> **Layer**: Core
> **Type**: Integration
> **Estimate**: M
> **Manifest Version**: 2026-09-20.1
> **Last Updated**:

## Context

**GDD**: `design/gdd/save-load.md`
**Requirement**: `TR-save-002`, `TR-save-003`

**Governing ADRs**: ADR-0005: Save Format (primary), ADR-0003
**ADR Decision Summary**: MessagePack-CSharp with AOT resolvers + LZ4; string keys; `Extras` bag on every record; JSON+gzip fallback behind the same `ISaveable` interfaces if AOT fails.

**Engine**: Unity 6000.6.1f1 | **Risk**: MEDIUM
**Engine Notes**: verify MessagePack-CSharp version compatible with Unity 6.6 (.NET Standard 2.1 / .NET profile) and that its source generator or `mpc` output works under IL2CPP. Record the outcome in Allowed Libraries.

**Control Manifest Rules (Persistence)**:
- Required: string keys; `Extras`; versioned sections.
- Forbidden: indices; Tag expansions; RNG state.

## Acceptance Criteria

- [ ] MessagePack-CSharp pinned in `Packages/manifest.json`; an IL2CPP Windows build serializes and deserializes a test record with AOT resolvers (or the JSON fallback decision is recorded and implemented instead).
- [ ] `SaveDocument` schema v1: `header{schemaVersion, gameVersion, addons[], marketBinding, lastSaveWallTime, tickIndex}`, sections `world`, `containers`, `mech`, `market`, `ledger`, `unlocks`, `tasks`, `extras`.
- [ ] Every record type has `IDictionary<string, object> Extras`; unknown keys in input survive a round-trip byte-for-byte in value.
- [ ] `ItemId` serializes as canonical string; a reserved Tag-matched input serializes as the concrete ItemId (GDD AC 3).
- [ ] No RNG or time-control fields exist in any section (reflection test over record types).
- [ ] Debug menu `Equilibrium/Save/Export JSON` writes a readable JSON of the current document.

## Implementation Notes

Custom `IMessagePackFormatter` for records that captures unknown map keys into `Extras` and writes them back. Use `MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray)`. Keep `ISaveable` in Simulation; file IO in Presentation.

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
**Status**: [ ] Not yet created

## Dependencies

- Depends on: item-registry 003
- Unlocks: Story 002

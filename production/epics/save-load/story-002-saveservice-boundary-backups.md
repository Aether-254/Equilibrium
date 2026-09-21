# Story 002: SaveService with tick-boundary capture, atomic write, and backups

> **Epic**: Save / Load
> **Status**: Ready
> **Layer**: Core
> **Type**: Integration
> **Estimate**: M
> **Manifest Version**: 2026-09-20.1
> **Last Updated**:

## Context

**GDD**: `design/gdd/save-load.md`
**Requirement**: `TR-save-001`, `TR-save-004`, `TR-save-005`

**Governing ADRs**: ADR-0005 (primary), ADR-0001, ADR-0008
**ADR Decision Summary**: capture at `TickPump` boundary; temp+rename; rotate 3 backups; `marketBinding` header immutable without credential.

**Engine**: Unity 6000.6.1f1 | **Risk**: MEDIUM
**Engine Notes**: `Application.persistentDataPath` on Windows; `File.Replace`/rename semantics.

**Control Manifest Rules (Persistence)**:
- Required: boundary-only; atomic write; backups.

## Acceptance Criteria

- [ ] Save requested mid-tick is written after that tick's Emit and reflects post-commit state (GDD AC 8).
- [ ] Two save requests in one tick coalesce into one write.
- [ ] Write goes to `save.tmp` then renames to `save.eqs`; previous `save.eqs` rotates to `.1`, `.1` to `.2`, `.2` to `.3`.
- [ ] Full round-trip: every `ISaveable` section equals its pre-save snapshot after load (GDD AC 1) using the fake world plus container and grid sections available so far.
- [ ] World created with `RemoteServer` binding offers no path to switch to `LocalNpc` (API has no setter; only `ApplyMigrationCredential` stub throws `NotSupported`) (GDD AC 9).
- [ ] Autosave every `autosaveIntervalTicks` (default 300).

## Implementation Notes

`SaveService.RegisterSaveable(ISaveable)`; on boundary, iterate saveables in registration order writing sections by `SectionKey`. Load is the mirror. Keep IO async off the main thread after capture (capture is synchronous at the boundary).

## Out of Scope

- Story 003: migrations, validation refusals, UnknownBlock

## QA Test Cases

- **AC-1**: request during Collect -> file tickIndex == post-commit tick
- **AC-3**: three consecutive saves -> `.1/.2/.3` exist with descending tickIndex
- **AC-4**: round-trip deep equality
- **AC-5**: attempt binding change -> not possible via API

## Test Evidence

**Story Type**: Integration
**Required evidence**: `Assets/Tests/EditMode/SaveLoad/SaveServiceTests.cs`; PlayMode file rotation test
**Status**: [ ] Not yet created

## Dependencies

- Depends on: Story 001; tick-simulation 005; grid-world 003; container 003
- Unlocks: Story 003

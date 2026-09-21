# Epic: Save / Load

> **Layer**: Core
> **GDD**: design/gdd/save-load.md
> **Architecture Module**: SaveService, ISaveable, ISaveMigration (Equilibrium.Simulation); file IO in Equilibrium.Presentation
> **Status**: Ready
> **Priority**: MVP
> **Engine Risk**: MEDIUM (MessagePack-CSharp AOT on Unity 6.6 IL2CPP — spike first)
> **Manifest Version**: 2026-09-20.1
> **Stories**: Not yet created — run `/create-stories save-load`

## Overview

Implement tick-boundary saves as MessagePack + LZ4 documents with string keys, `Extras` bags on every record, ordered migrations with fixture corpus, rotating backups with atomic writes, schema/world-size refusal rules, `UnknownItem`/`UnknownBlock` hydration, market-binding header, and a debug JSON export. Every persisted system implements `ISaveable`. The first story is a compatibility spike; if MessagePack fails on IL2CPP the JSON+gzip fallback is used behind the same interfaces.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0005: Save Format | MessagePack AOT; migrations; unknown-field preservation | MEDIUM |
| ADR-0001: Tick Transaction Engine | Boundary hook | LOW |
| ADR-0003: Item Identity | Canonical ItemId strings; UnknownItem | MEDIUM |
| ADR-0004: World Data Model | Key-based serialization | LOW |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-save-001 | Boundary-only save; all-or-nothing load | ADR-0005, ADR-0001 |
| TR-save-002 | schemaVersion; migrations; unknown fields kept; refuse newer | ADR-0005 |
| TR-save-003 | Full ItemIds; no Tag expansion; concrete reserved ids; no RNG | ADR-0005, ADR-0002 |
| TR-save-004 | marketMode binding immutable without credential | ADR-0005, ADR-0008 |
| TR-save-005 | Rotating backups; atomic write | ADR-0005 |

## Definition of Done

- save-load acceptance criteria as EditMode tests; fixture corpus across migrations in CI.
- IL2CPP Windows build round-trips a save.
- MessagePack-CSharp pinned in `Packages/manifest.json` and Allowed Libraries updated with the version.

## Next Step

Run `/create-stories save-load`.

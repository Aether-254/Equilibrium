# Epic: Item Registry

> **Layer**: Foundation
> **GDD**: design/gdd/item-registry.md
> **Architecture Module**: ContentRegistry, AddonLoader, Validator (Equilibrium.Content); ScriptableObject defs + exporter, Addressables load (Equilibrium.Content.Unity)
> **Status**: Implemented (pending creator Unity test run)
> **Priority**: MVP
> **Engine Risk**: MEDIUM (Addressables 2.x label load)
> **Manifest Version**: 2026-09-20.1
> **Stories**: 4 (table below)

## Overview

Implement item identity and content loading: interned `ItemId` with canonical namespaced string, `ItemDef`/`RecipeDef`/`TagDef`/`MachineDef`/`StructureDef`/`UnlockNodeDef`/`MarketItemDef` records, hierarchical Tag graph with reference resolution and remove-wins edits, Addon manifest DAG with topological load and rejection codes, `UnknownItem` factories, and a single validator shared by the ScriptableObject export path and the JSON Addon path. Includes the editor export of base content to the reference Addon pack and the Tier 1 content set (log, plank, wood fuel, engine, shaft, gears 8/16/32, saw, chest, belt, accessor, blocker).

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-0003: Item Identity, Content Format, Addon Loading | SO base + JSON addons into one registry; shared validator | MEDIUM |
| ADR-0005: Save Format | Canonical ItemId strings; UnknownItem hydrate contract | MEDIUM |

## GDD Requirements

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-item-001 | ItemId regex; default namespace omission only in config/display | ADR-0003 |
| TR-item-002 | Duplicate ItemId hard error; Addon refuses load | ADR-0003 |
| TR-item-003 | Hierarchical Tags; references; cycle error; remove-wins; child cascade | ADR-0003 |
| TR-item-004 | Addon manifest; topological load; cycle/undeclared-modify rejection | ADR-0003 |
| TR-item-005 | UnknownItem preserves id, count, extra data | ADR-0003, ADR-0005 |

## Definition of Done

- All item-registry acceptance criteria as EditMode tests using the JSON source.
- Round-trip test SO -> JSON -> parse equality.
- Tier 1 content authored and loads under 200 ms.
- `Equilibrium.Content.asmdef` has no UnityEngine reference.

## Stories

| # | Story | Type | Status |
|---|-------|------|--------|
| 001 | ItemId parsing, canonicalization, and interning | Logic | Complete |
| 002 | ItemDef records and hierarchical Tag graph | Logic | Complete |
| 003 | Addon manifest, dependency graph, and UnknownItem | Logic | Complete |
| 004 | ScriptableObject and JSON content sources with shared validator | Integration | Complete |

## Next Step

Run `/story-readiness production/epics/item-registry/story-001-itemid-parsing-interning.md` then `/dev-story`.

# Story 003: String-key port binding, overflow container, and prototype removal

> **Epic**: Container
> **Status**: Ready
> **Layer**: Core
> **Type**: Integration
> **Estimate**: S
> **Manifest Version**: 2026-09-20.1
> **Last Updated**:

## Context

**GDD**: `design/gdd/container.md`
**Requirement**: `TR-cont-004`

**Governing ADRs**: ADR-0004 (primary), ADR-0005
**ADR Decision Summary**: definitions carry `containerKey`/`slotKey`; runtime resolves to indices at load; saves emit keys.

**Engine**: Unity 6000.6.1f1 | **Risk**: LOW

**Control Manifest Rules (Simulation / Persistence)**:
- Forbidden: index-based serialization.

## Acceptance Criteria

- [ ] Port bound to `slotKey: "input_a"` resolves to the correct slot after the content slot array is reordered (GDD AC 7).
- [ ] Missing `slotKey` after a content update marks the port `PortBindingMissing`; machine enters Blocked (via machine-processing when available; here the flag is set).
- [ ] Content shrink with more filled slots than exist moves extras into an overflow container flagged for UI; nothing deleted.
- [ ] Benchmark: 1000 containers with one insert each per tick under 0.5 ms (GDD AC 8).
- [ ] Prototype `ItemStack.cs`, `ItemContainer.cs`, `ContainerTransfer.cs`, `IContainer.cs`, `IItemContainerOwner.cs` deleted from `Assets/Script/` and no scene/prefab references remain.

## Implementation Notes

Key resolution happens in a `BindingResolver` run when a block instance is created from its def. Overflow container is a per-block optional container with key `_overflow`. Before deleting prototype scripts, check `Main.unity` and prefabs for missing-script warnings and remove the components.

## Out of Scope

- Save/load round-trip of keys (save-load epic)

## QA Test Cases

- **AC-1**: reorder slot defs, resolve -> same logical slot
- **AC-2**: remove slot def -> `PortBindingMissing`
- **AC-3**: shrink 3 -> 2 slots with 3 filled -> overflow holds 1
- **AC-4**: benchmark
- Manual: Unity console shows no missing-script warnings on opening Main.unity

## Test Evidence

**Story Type**: Integration
**Required evidence**: `Assets/Tests/EditMode/Container/BindingTests.cs`; console screenshot in `production/qa/evidence/container-prototype-removal-evidence.md`
**Status**: [ ] Not yet created

## Dependencies

- Depends on: Story 002
- Unlocks: save-load, conveyor, machine-processing epics

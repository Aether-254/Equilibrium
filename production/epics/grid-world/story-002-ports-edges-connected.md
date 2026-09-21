# Story 002: Ports, facing rotation, ingress edges, and connected predicate

> **Epic**: Grid World
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-09-20.1
> **Last Updated**:

## Context

**GDD**: `design/gdd/grid-world.md`
**Requirement**: `TR-grid-003`, `TR-grid-004`

**Governing ADRs**: ADR-0004
**ADR Decision Summary**: `PortTable` with kind, local facing, container/slot local index, acceptsDirectBelt; `EdgeTable` 4 ingress bits per cell; adjacency implicit; `Connected(a, outPort, out b, out inPort)`.

**Engine**: Unity 6000.6.1f1 | **Risk**: LOW

**Control Manifest Rules (Simulation)**:
- Forbidden: proximity-based connection lookup.

## Acceptance Criteria

- [ ] `worldFacing = (localFacing + orientation) mod 4` for every port (GDD formula).
- [ ] Default machine layout unrotated: left item-in, right item-out, top rotation/energy-in, bottom byproduct-out.
- [ ] `Connected` true only when adjacent, facings mutual, kinds compatible, ingress enabled (GDD AC 2); machine rotated 90 deg sends output S and the E belt receives nothing (GDD AC 3).
- [ ] Two output ports facing each other: not connected, no error.
- [ ] Belt end facing a chest with no accessor: not connected; conveyor will report `NoReceivingPort` (GDD AC 5).
- [ ] Edge at world boundary: not connected (GDD AC 7).
- [ ] `EdgeTable.SetIngress(cell, side, enabled)` flips one bit; `Connected` respects it.

## Implementation Notes

Compatibility matrix as a static table over `PortKind`. `Connected` computes the neighbor cell from facing, finds the neighbor block, scans its ports for one facing back with compatible kind and returns the first in stable port order.

## Out of Scope

- Blocker install/remove transactions (flow-blocker epic)
- Mechanical network graph (mechanical-power epic)

## QA Test Cases

- **AC-1**: local E, orientation 1 -> S
- **AC-3**: machine out E + belt at E facing E -> connected; rotate machine -> not connected
- **AC-4**: out vs out -> false
- **AC-7**: disable ingress W on the belt -> connected false; enable -> true

## Test Evidence

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/GridWorld/PortsAndConnectionsTests.cs`
**Status**: [ ] Not yet created

## Dependencies

- Depends on: Story 001
- Unlocks: conveyor, mechanical-power, flow-blocker epics

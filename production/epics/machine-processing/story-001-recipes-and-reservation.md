# Story 001: Recipe data, selection, and input reservation

> **Epic**: Machine Processing
> **Status**: In Progress — first powered saw line; complete epic acceptance still pending
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-09-20.1
> **Last Updated**: 2026-11-05

## Context

**GDD**: `design/gdd/machine-processing.md`
**Requirement**: `TR-mach-001`, `TR-mach-002`

**Governing ADRs**: ADR-0003 (primary), ADR-0001, ADR-0004
**ADR Decision Summary**: `RecipeDef` record from content; reservation flags on slots; consume on completion; cancel releases unless irreversible.

**Engine**: Unity 6000.6.1f1 | **Risk**: MEDIUM

**Control Manifest Rules (Simulation)**:
- Required: recipe values from content; reservation as transaction.

## Acceptance Criteria

- [ ] `RecipeDef{recipeId, machineId|machineTag, inputs[], outputs[], byproducts[], workRequired, allowMixedTagInputs, irreversible, acceptWhileWorking}` loads from content; `baseTicks` derived for display.
- [ ] Idle machine with inputs present reserves them and enters Working with `workDone = 0`; reserved counts excluded from `available`.
- [ ] Recipe selection: first applicable in stable order, or pinned.
- [ ] 5x `#ingot` with 3 iron + 2 copper and `allowMixedTagInputs=false` -> Starved `MissingInput:#ingot` (GDD AC 5).
- [ ] Cancel on non-irreversible releases reservations (GDD AC 8); on irreversible after start -> refused `Irreversible` (GDD AC 9).
- [ ] Player removes a reserved input -> machine Starved `ReservationBroken` (GDD AC 4).
- [ ] `acceptWhileWorking=false` makes feeding belts stall `MachineNotAccepting`.

## Implementation Notes

2026-11-05: Runtime subset implemented in ProcessingSystem, RecipeInputPlanner and
StraightMechanicalSolver, with Main placement/inspection integration. See
docs/architecture/machine-processing-runtime.md and
production/qa/evidence/machine-processing-evidence.md for exact coverage. Gears, full multi-source
networks, final animations, ledger events and performance acceptance remain pending.


Machine instance state (recipe id, workDone, reservations, pending batch) is a persistent attribute bag entry so removal carries it (GDD AC 6 tested in story 002). Tag-input matching: for each input, collect candidate slots by Tag; if mixing disallowed, require one ItemId to satisfy the full count.

## Out of Scope

- Story 002: work integration, completion, blocked output, states/ReasonCodes
- Story 003: sprites and end-to-end line

## QA Test Cases

- GDD AC 4, 5, 8, 9 verbatim
- Selection: two applicable recipes -> stable-order winner; pin overrides

## Test Evidence

**Story Type**: Logic
**Required evidence**: `Assets/Tests/EditMode/MachineProcessing/RecipeAndReservationTests.cs`
**Status**: [ ] Not yet created

## Dependencies

- Depends on: item-registry 004; container 002
- Unlocks: Story 002

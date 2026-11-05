# Grid World 001 Verification Evidence

Date: 2026-09-23
Story: `production/epics/grid-world/story-001-world-tables-occupancy.md`
Engine: Unity 6000.6.1f1

## Outcome

APPROVED WITH CONDITION

All GW-001 acceptance tests pass. The remaining condition is unrelated to Grid World: the Unity-only snapshot guard test is skipped because `EQ_DEBUG_SNAPSHOT` is not defined in the Unity test assembly configuration.

## Focused Pure C# Check

Command:

```powershell
dotnet test .\tools\purecheck\Equilibrium.PureCheck.csproj `
  --configuration Debug `
  --no-restore `
  --filter "FullyQualifiedName~GridWorld"
```

Result:

```text
Total: 10
Passed: 10
Failed: 0
10 dirty chunk rebuild: 0.1112 ms
```

## Full Pure C# Regression

Command:

```powershell
dotnet test .\tools\purecheck\Equilibrium.PureCheck.csproj `
  --configuration Debug `
  --no-restore
```

Result:

```text
Total: 82
Passed: 79
Skipped: 3 Unity-only assembly audits
Failed: 0
```

## Unity EditMode

Command:

```powershell
unity test "D:\UnityProj\Equilibrium" `
  --editor-version 6000.6.1f1 `
  --mode EditMode `
  --report-format nunit `
  --output "$env:LOCALAPPDATA\EquilibriumCodex\editmode-results.xml" `
  --timeout 600 `
  --format json `
  --non-interactive
```

Result:

```text
Total: 92
Passed: 91
Skipped: 1
Failed: 0
Duration: 0.3776172 s
```

Focused Grid World result:

```text
Total: 10
Passed: 10
Failed: 0
10 dirty chunk rebuild: 0.0570 ms
```

Skipped test:

```text
TransactionCommitTests.test_live_read_during_collect_expected_snapshot_violation_in_debug
Reason: EQ_DEBUG_SNAPSHOT not defined; guard compiled out.
```

## Initial Failure and Fix

The first Unity run did not start tests because `Equilibrium.Editor.asmdef` did not reference the installed Sprite Editor assembly used by `ShaftSpriteBakerEditor.cs`. The reference was resolved from the active `com.unity.2d.sprite` package and added as `Unity.2D.Sprite.Editor`.

The next Unity run found six public mutable fields in the new Simulation world types. They were converted to readonly fields or properties with non-public setters so state mutation remains behind the writer boundary.

## Acceptance Mapping

| Acceptance criterion | Evidence |
| --- | --- |
| Out-of-bounds is immutable Void | `test_out_of_bounds_expected_void_not_empty` |
| Block table fields and ranges | `test_block_table_expected_definition_orientation_controller_ranges_and_bag` |
| Occupied placement is unchanged | `test_place_on_occupied_cell_expected_cell_occupied_unchanged` |
| Remove yields drop; rotate commits next tick | `test_remove_expected_cell_empty_and_drop_item`, `test_structural_transaction_expected_visible_only_after_commit` |
| O(dirty) immutable chunk rebuild under 0.2 ms | `test_snapshot_delta_expected_immutable_changed_chunk_views`, `test_snapshot_dirty_rebuild_512_expected_under_0_2_ms` |
| Compaction preserves live refs and pending dirty deltas | `test_compaction_recycles_dead_indices_expected_reuse` |

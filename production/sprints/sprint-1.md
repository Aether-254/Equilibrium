# Sprint 1 — 2026-09-21 to 2026-10-02

## Sprint Goal
Stand up the Foundation layer in the main Unity project: the pure C# tick transaction engine and the content registry, with assembly definitions, EditMode tests, and the two engine-compatibility spikes started, so every Core epic can begin in Sprint 2.

## Capacity
- Total days: 10 (solo developer, 2 weeks)
- Buffer (20%): 2 days reserved for Unity 6.6 surprises and asmdef churn
- Available: 8 days

## Tasks

### Must Have (Critical Path)
| ID | Task | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria |
|----|------|-------------|-----------|-------------|-------------------|
| TS-001 | Create assembly definitions and TickEngine skeleton | unity-specialist / engine-programmer | 1.0 | None | 7 asmdefs compile; Simulation/Content/Editing have no UnityEngine ref; phase order test passes; TickPump advances whole ticks |
| TS-002 | Transaction buffers, snapshot, and atomic commit | engine-programmer | 1.0 | TS-001 | Zero-alloc buffer; snapshot isolation; failed validation leaves state unchanged; 10k tx < 4 ms |
| TS-003 | Priority, dependency ordering, and conflict resolution | engine-programmer | 1.0 | TS-002 | GDD AC 3, 5, 6 pass; cycle -> TransactionCycle; dependency cascade |
| TS-004 | CSPRNG tie-break source | engine-programmer | 0.5 | TS-003 | Non-reproducible sequences; 48-52% fairness; failure halts; no ITieBreakSource in Editing |
| TS-005 | Event emission, save boundary hook, and mutation audit | engine-programmer | 0.5 | TS-004 | One event per tx; boundary hook after Emit; world tables internal |
| IR-001 | ItemId parsing, canonicalization, and interning | gameplay-programmer | 0.5 | TS-001 | `wood` -> `equilibrium:wood`; invalid table rejected; handle interning |
| IR-002 | ItemDef records and hierarchical Tag graph | gameplay-programmer | 1.0 | IR-001 | Duplicate hard error; `/` inclusion; remove-wins cascade; TagCycle |
| IR-003 | Addon manifest, dependency graph, and UnknownItem | gameplay-programmer | 1.0 | IR-002 | Rejection codes; topological order; UnknownItem hydrate/restore |
| IR-004 | ScriptableObject and JSON content sources with shared validator | unity-specialist / unity-addressables-specialist | 1.0 | IR-003 | SO<->JSON round-trip equality; identical error codes; Tier 1 pack loads < 200 ms |

Must Have total: 7.5 days.

### Should Have
| ID | Task | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria |
|----|------|-------------|-----------|-------------|-------------------|
| SL-001 | MessagePack compatibility spike and SaveDocument schema v1 | unity-specialist | 1.0 | IR-003 | IL2CPP round-trip or documented JSON fallback; schema v1 records with Extras; no RNG fields |
| UI-001 | UI Toolkit shell, data-binding spike, and inspection status bar | unity-ui-specialist | 1.0 | TS-005 | Binding spike result recorded; HudRoot + panel shell open/close; status bar visible across tabs |

### Nice to Have
| ID | Task | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria |
|----|------|-------------|-----------|-------------|-------------------|
| GW-001 | World tables, occupancy, and orientation | engine-programmer | 1.0 | TS-005, IR-002 | CellOccupied; dirty chunks; 512x512 snapshot < 0.2 ms |
| ENG-REF | `/setup-engine refresh` for Unity 6.4-6.6 deltas | technical-director | 0.5 | None | engine-reference docs updated; Last Docs Verified stamped |

## Carryover from Previous Sprint
| Task | Reason | New Estimate |
|------|--------|-------------|
| — | First sprint | — |

## Risks
| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| `noEngineReferences: true` breaks on a needed math type | Medium | Medium | Use `System.Numerics`; if blocked, allow `UnityEngine.CoreModule` only, record in ADR-0007 consequences |
| Moving the shaft baker into `Equilibrium.Editor` breaks `Main.unity` references | Medium | Low | Keep the MonoBehaviour in Presentation, move only bake logic; verify scene opens clean |
| Addressables 2.x API differs from training data (IR-004) | High | Medium | Read installed package docs first; label-load path isolated behind `IContentSource` |
| System.Text.Json source generation unavailable on this .NET profile (IR-004) | Medium | Low | Reflection-based serializer fallback; test on IL2CPP later |
| MessagePack AOT fails on IL2CPP (SL-001) | Medium | High | JSON+gzip fallback already specified in ADR-0005; decision recorded in Allowed Libraries |
| UI Toolkit runtime binding immature (UI-001) | Medium | Medium | Manual view-model update fallback behind `IUiPresenter` |
| Bash tooling slow on this machine | High | Low | Use powershell.exe for batch operations (see memory) |

## Dependencies on External Factors
- Unity 6000.6.1f1 editor available locally with URP 17.6 and Input System 1.20.0 (present).
- game-ci secrets (`UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`) must be added to the GitHub repo for `.github/workflows/tests.yml` to run; local test runs are sufficient for this sprint.
- MessagePack-CSharp package source (OpenUPM or UPM git) reachable.

## Definition of Done for this Sprint
- [ ] All Must Have tasks completed
- [ ] All tasks pass acceptance criteria
- [ ] QA plan exists (`production/qa/qa-plan-sprint-1.md`)
- [ ] All Logic/Integration stories have passing EditMode tests under `Assets/Tests/EditMode/`
- [ ] Smoke check passed (`/smoke-check sprint`)
- [ ] QA sign-off report: APPROVED or APPROVED WITH CONDITIONS (`/team-qa sprint`) — solo mode: creator self-sign-off recorded in `production/qa/`
- [ ] No S1 or S2 bugs in delivered features
- [ ] Design documents updated for any deviations (tick-simulation.md, item-registry.md)
- [ ] Code reviewed (creator self-review) and merged to main

## Notes
- No vertical slice or prototype; TS-001 establishes the assemblies inside the main project and later sprints refactor `Assets/Script/` in place.
- Sprint 2 candidates: grid-world 001-003, container 001-003, mechanical-power 001-002, save-load 002.

# Item Registry

> **Status**: Approved (creator self-review 2026-09-20; no independent /design-review)
> **Author**: creator + design session
> **Last Updated**: 2026-09-20
> **Implements Pillar**: Depth Is Progress (stable identities let later tiers reinterpret early items), Factory and Economy Are One (one identity from recipe to market)
> **Normative Source**: design/confirmed-mechanics-spec.md s2, s2A, s2B
> **Review**: specialists and CD-GDD-ALIGN not consulted — Solo mode. Review manually before production.

## Overview

The item registry defines what an item *is*: a stable, namespaced `ItemId`, an `ItemDef` with properties such as `maxStack`, and membership in hierarchical `Tag`s used by recipes, slot filters, and the market. It also governs how base content and Addons register items, how load order and conflicts are resolved, and how saves survive a missing Addon via `UnknownItem` placeholders. Players never open the registry, but every recipe, filter, price, and blueprint cost references it, and the Addon system that lets the game grow into later fidelity tiers stands on it.

## Player Fantasy

Indirect. The player feels the registry as consistency: iron ingot is the same thing in a chest, a recipe, a slot filter, and a market listing; a Tag like `#ingot` means the same set everywhere. For modders and later content, it is the promise that adding a farming Addon does not corrupt the base factory.

## Detailed Design

### Core Rules

**ItemId**
1. Every item has an `ItemId` of the form `namespace:path`, lowercase, no spaces, matching `^[a-z0-9][a-z0-9._-]*:[a-z0-9][a-z0-9._/-]*$`.
2. `equilibrium:` is the base namespace and may be omitted in config input and display; runtime and saves MUST store the fully qualified form.
3. Non-default namespaces can never be abbreviated.
4. Saves, recipes, market, blueprint costs, and logistics filters MUST use full `ItemId`s, never display names.
5. An invalid `ItemId` fails registration of that content entry.
6. Duplicate `ItemId` is a hard error: the second registration does not overwrite; the Addon that caused the duplicate refuses to load.

**ItemDef**
7. Each `ItemId` maps to one `ItemDef` containing at minimum: `maxStack` (int >= 1), display name key, icon reference, and an optional list of tradeability flags (market-interface GDD).
8. `ItemDef` fields are static content; per-instance mutable data lives in item instance state (multiblock/machine drops carry persistent attributes, spec s3.3).

**Tags**
9. A Tag is a named list prefixed with `#`, e.g. `#equilibrium:ingot`; `equilibrium:` may be omitted the same way as ItemIds.
10. Tag names are singular and carry no affixed form.
11. `/` in a Tag path is parent-child inclusion: members of `#a:b/c/d` are members of `#a:b/c` and `#a:b`.
12. Tag lists may contain `ItemId`s and references to other Tags. References form a dependency graph built at content load; a cycle is a hard error and all content in the cycle refuses to load.
13. `#common:` is reserved for Addons/Mods; the base game neither supports nor maintains it.
14. Addons may add or remove Tag members. Remove beats add for the same member. Removing an item from a parent Tag MUST also remove it from every child Tag through which it inherited that parent.
15. Saves MUST NOT persist expanded Tag membership; they persist concrete `ItemId`s. Reserved recipe inputs persist the chosen concrete `ItemId`.

**Addon loading**
16. Each Addon declares a unique `namespace` in its config; missing namespace refuses load.
17. An Addon that modifies another must declare the target namespace(s); undeclared cross-Addon modification refuses load of the modifier.
18. Dependencies and modifications form a directed graph. Load in topological order. Any cycle refuses load of every Addon in the cycle. If a target refuses load, dependents refuse load too.
19. When a save references an `ItemId` whose namespace is absent, the item becomes `UnknownItem` preserving the original full `ItemId`, count, and recoverable extra data. Restoring the Addon restores the item; timing is TBD.

### States and Transitions (registry lifecycle)

| State | Transition | Next |
|---|---|---|
| Unloaded | game start | Loading base |
| Loading base | base content valid | Loading addons |
| Loading base | base invalid | Fatal |
| Loading addons | topo order complete | Ready |
| Loading addons | cycle / missing namespace / duplicate | Ready (offending Addons marked Rejected, listed in UI) |
| Ready | save load with unknown namespace | Ready (UnknownItem instances created) |

### Interactions with Other Systems

| System | Direction | Interface |
|---|---|---|
| container | out | `ItemDef.maxStack` for effective capacity; Tag membership for slot filters |
| machine-processing | out | recipe inputs by ItemId or Tag; Tag input matching policy |
| market-interface / market-server | out | tradeable ItemIds; compatibility profiles keyed by namespace set |
| save-load | out | full ItemId serialization; UnknownItem contract |
| item-io-accessor, flow-blocker | out | ItemId filters |
| tech-unlock, tutorial-task | out | content gating by ItemId |

## Formulas

The Tag membership formula is defined as:

`members(#ns:p) = direct(#ns:p) ∪ ⋃_{child ∈ children(#ns:p)} members(child) ∪ ⋃_{ref ∈ refs(#ns:p)} members(ref) − removed(#ns:p)`

**Variables:**
| Variable | Symbol | Type | Range | Description |
|---|---|---|---|---|
| direct members | direct | set<ItemId> | - | ItemIds listed on the Tag itself |
| children | children | set<Tag> | - | Tags whose path extends this path by one or more `/` segments |
| references | refs | set<Tag> | - | Tags referenced in the list |
| removals | removed | set<ItemId> | - | Items removed by any Addon at this Tag or any ancestor |

**Output Range:** Finite set; computed once at load, invalidated on Addon change.
**Example:** `#ingot/iron = {iron_ingot}`, `#ingot = {} ∪ #ingot/iron` so `iron_ingot ∈ #ingot`. Addon removes `iron_ingot` from `#ingot`; rule 14 removes it from `#ingot/iron` as well.

The effective stack rule is owned by the container GDD (`effectiveCapacity = min(slot.capacity, itemDef.maxStack)`); this GDD supplies `itemDef.maxStack`.

## Edge Cases

- **If an Addon declares `namespace: equilibrium`**: refuse load (`NamespaceReserved`).
- **If two Addons both add and one removes the same Tag member**: remove wins (rule 14).
- **If a Tag references itself**: cycle; refuse load of that Tag's content.
- **If a Tag path contains `//` or trailing `/`**: invalid ItemId regex fails; registration error.
- **If a save contains an `UnknownItem` and the Addon returns with the item removed from that version**: item stays `UnknownItem`; never silently deleted.
- **If `maxStack` is 0 or negative in an ItemDef**: registration error.
- **If display name key is missing**: register with fallback to full ItemId as display text; warning.
- **If the base game itself has an ItemId collision**: fatal at startup; not recoverable.

## Dependencies

| System | Kind | Notes |
|---|---|---|
| none upstream | - | Foundation |
| tick-simulation | soft | Registry loads before the first tick; not tick-driven |
| container, machine-processing, market-interface, save-load, item-io-accessor, flow-blocker, tech-unlock | downstream hard | consume identities and Tags |

## Tuning Knobs

| Knob | Default | Safe Range | Too Low | Too High |
|---|---|---|---|---|
| default maxStack | 64 | 1..3000 | conveyor groups tiny | slot capacity dominates |
| Tag graph depth limit | 16 | 4..64 | legit hierarchies rejected | pathological load time |
| Addon load timeout | 30 s | 5..300 | large packs fail | hang appears as freeze |

## Visual/Audio Requirements

None. Rejected Addons are listed in a text panel.

## UI Requirements

Addon list showing load state and rejection ReasonCode. UnknownItem renders with a distinct placeholder icon and full ItemId tooltip.

## Acceptance Criteria

- **GIVEN** config input `wood`, **WHEN** parsed, **THEN** runtime ItemId is `equilibrium:wood`.
- **GIVEN** `Iron_Ingot:foo`, **WHEN** parsed, **THEN** registration fails with `InvalidItemId`.
- **GIVEN** two Addons registering `farmer_addon:seed`, **WHEN** loading, **THEN** the first wins, the second Addon is Rejected with `DuplicateItemId`.
- **GIVEN** `#ingot/iron` contains `iron_ingot`, **WHEN** querying `#ingot`, **THEN** `iron_ingot` is a member.
- **GIVEN** Addon B removes `iron_ingot` from `#ingot`, **WHEN** querying `#ingot/iron`, **THEN** `iron_ingot` is not a member.
- **GIVEN** Tag A references B and B references A, **WHEN** loading, **THEN** content of both refuses to load with `TagCycle`.
- **GIVEN** Addon B depends on A and A refuses load, **WHEN** loading, **THEN** B refuses load with `DependencyRejected`.
- **GIVEN** a save with `farmer_addon:seed x10` and no farmer_addon, **WHEN** loaded, **THEN** an `UnknownItem` with original ItemId and count 10 exists in the same slot.
- **GIVEN** a reserved recipe input matched via `#ingot`, **WHEN** saved and reloaded, **THEN** the save contains the concrete ItemId, not the Tag.

## Open Questions

| # | Question | Owner | Target |
|---|---|---|---|
| 1 | UnknownItem recovery timing and migration flow | systems-designer | Alpha |
| 2 | `#common:` cooperation rules | Addon system design | Alpha |
| 3 | Whether ItemDef carries physical properties (mass, density) now or in Tier 3 | game-designer | Tier 3 |
| 4 | Content file format (JSON vs ScriptableObject) | technical-director (ADR) | before implementation |

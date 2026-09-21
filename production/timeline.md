# Timeline

Equilibrium is an open-ended personal Unity learning project. No release date or commercial schedule is currently defined.

## Current Stage

Core-mechanics definition following adoption of an early playable Unity learning prototype.

The `prototype` label records the observed implementation maturity. The project uses CGS `fast-prototype` mode so hobby learning work is not blocked by heavyweight production gates.

# Milestones

Current milestone: define and validate the first mechanical-fidelity and factory-economy playable loop without reorganizing existing Unity content.

## Current Milestone

Define the smallest playable loop that connects realistic mechanical transmission, production, storage or sale, and visible economic results, while keeping the existing Unity project in place.

### Evidence already present

- `Assets/Scenes/Main.unity` is the enabled build scene.
- Grid placement/removal and machine instantiation exist.
- Item container, production, storage, and transfer primitives exist.
- Machine and conveyor prefabs exist.
- URP 2D and a separate shaft render path exist.
- A 128-frame shaft sprite bake and playback experiment exists.

# Next Validation Gate

1. Open the project with Unity `6000.6.1f1`.
2. Confirm scripts compile without Console errors.
3. Open `Assets/Scenes/Main.unity` and verify missing references.
4. Assign or deliberately defer `MachinePlacer.conveyorPrefab`.
5. Exercise place/remove behavior and the shaft animation in Play Mode.
6. Add focused EditMode tests for the item container and transfer domain.

## Non-goals for Adoption

- No Unity folder restructuring.
- No scene, prefab, script, or asset renames.
- No restoration of deleted template content.
- No speculative GDD, market document, or architecture document.
- No release schedule or commercial milestone commitment.

# Risks

- The existing worktree is dirty and includes Unity asset and settings changes.
- Conveyor placement has an unassigned prefab reference in the current main scene.
- Unity compile and Play Mode behavior remain unverified because no connected Editor was available during inventory.
- No project gameplay tests currently exist.

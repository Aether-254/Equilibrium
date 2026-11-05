# Machine processing runtime

Updated: 2026-11-05. Scope: the existing Main scene's first powered production line.
Normative references: `design/confirmed-mechanics-spec.md`, `design/gdd/machine-processing.md`,
`design/gdd/mechanical-power.md`. This implementation is an incremental subset, not completion of either epic.

## Runtime and transaction ownership

`ProcessingSystem` is a Simulation batch planner and logistics port policy. FactorySession registers
logistics, processing, then editing. The explicit commit dependencies are:

`LogisticsBatch -> ProcessingBatch -> FactoryEditBatch`.

Processing copies logistics' resolved slot plan, computes fuel use, recipe reservations, work, output
transfers and completed batches on scratch state. WorldWriter applies its slots, reservations and
persistent bags together after verifying the captured world revision plus the logistics commit.
Planning does not mutate the world. Construction/removal/configuration remains a later commit.
This composition deliberately allows arrived inputs to be reserved in this tick, but a new reservation
receives its first work on the following tick. Only output present at Snapshot can be pushed to belts;
newly completed products stay in the machine until a subsequent tick.

## Recipe lifecycle

- Recipes come from ContentRegistry in stable order. The existing saw converts `equilibrium:log` x1
  into `equilibrium:plank` x2 for 1350 J; its content torque demand is 30 N*m.
- RecipeInputPlanner uses a capacity-flow match for overlapping ingredient filters. Non-mixed Tag
  ingredients select one concrete ItemId; mixed ingredients may reserve several matching ItemIds.
  Reservations record concrete item handles and local slot indices, not an unresolved Tag.
- Starting reserves inventory without consuming it. Ordinary extraction excludes the reserved count.
  Working integrates received watts for the one-second tick. No power preserves input and progress.
- Completion consumes the reservation once. All main products and rolled byproducts must fit together;
  otherwise the complete result stays in a persistent pending batch with Blocked/OutputFull. Retrying
  does not consume input or roll byproducts again. One machine completes at most once per tick.
- Surplus work is capped at one recipe's work and retained only for that recipe. Zero-work recipes
  complete on their first Working tick, even without power, as specified by the existing content rule.
- Idle/NoInput, Starved/MissingInput, Working and Blocked are visible. Broken reservations become
  Starved/ReservationBroken. Working input admission honors acceptWhileWorking and recipe item filters.
- Cancellation releases reservations and resets work; irreversible recipes refuse it. The runtime
  exposes a cancel action. Recipe pinning is a WorldWriter boundary API, not a finished recipe-picker UI.
- Utilization is a rolling 60-tick ratio of powered Working ticks. Completed-batch count is cumulative.

Machine outputs push whole groups to an empty compatible adjacent belt after logistics resolves. They
never merge with belt inventory. Main output is on the right, input on the left, rotation input above;
all rotate with the block. A machine retains undelivered output in its output container. A finished
batch that cannot enter the output containers is Blocked and requests no further torque.

## First drivetrain

Fuel engine and shaft block definitions use explicit adjacent rotation ports. An unrotated engine
accepts fuel from the left and sends rotation downward; a straight shaft connects top and bottom.
`R` rotates either block. A saw below the shaft accepts rotation on its top edge.

Mechanical parameters are in Tier1Content ItemDef extras: engine omega_src 10 rad/s, tau_max 40 N*m,
eta_engine 0.25; shaft eta_shaft 0.99. Wood's existing EnergyJ is 8000 J/item.
For the single-source, ratio-1 path, this follows the current GDD formulas:

- eta_path is the product of shaft efficiencies.
- source demand is the sum of working machine torque / eta_path.
- demand strictly above source capacity stalls the component and burns no fuel.
- received watts = machine torque * source speed. Path loss is already charged in source demand.
- fractional fuel debt = delivered source joules / (fuel EnergyJ * engine efficiency).

Idle/Blocked machines request no torque. Fuel is charged only for source power delivered that tick.
If the remaining fuel energy cannot cover the entire one-second tick, delivered work is capped to
that remaining energy; the next tick reports NoFuel. This prevents the fractional accumulator from
granting an extra full tick of free energy. It is not an inertia or partial-speed simulation.

Creator decision 2026-11-05 corrects the former double-counting of transmission loss. One engine plus
one shaft supplies a working saw with 300 W and requires about 30.303 N*m (303.03 W) at the source.
One wood supplies 8000 * 0.25 * 0.99 = 1980 J of total machine work through that shaft, including
the last partial fuel tick. Machine workRequired and torque demand are unchanged.
Disconnecting the shaft stops work on the following tick. No nearby-object search is used.

This solver does not implement gears, gear loops, branching shaft blocks, multiple compatible source
sharing, or topology caches. A component with multiple active sources is conservatively faulted in
this slice; full source compatibility/sharing remains part of the mechanical epic. The new solver
allocates scratch data per tick; no large-network performance acceptance is claimed.

## Removal, presentation and boundaries

PersistentBag now carries a cloned MachineState and fractional fuel debt. Removal/replacement restores
recipe identity, concrete reservations, work, pending products and observations; direction is new block
state. CaptureBag returns a detached snapshot, including named container/slot bindings and each slot's
reserved count. Later ticks cannot modify the captured machine state. Re-placement restores those
bindings within the same registry and block definition. These are live-session bags, not new MessagePack
save fields: the live world still has no save capture/hydration, and exiting Play Mode discards it.

Main's toolbar adds Saw (1), Fuel engine (2), Shaft (3), all consuming finite warehouse items. The learning
preset adds 4 saws, 4 engines, 32 shafts and 128 logs. These are provisional stock settings, not balance
or progression decisions. The inspector offers material deposits, output collection, cancel, slot
reservations, work, power, torque load, completed batches and utilization. Deposits use the actual
input/fuel port policy, so wood cannot be fed into the saw's log input.

SAW/ENGINE/SHAFT, IN/OUT/P and state glyphs reuse the existing labelled prototype sprite. They are
temporary visuals; no new production artwork, rotating shaft animation, market, unlock system or
full machine-inspector tabs were introduced. Main scene and existing art are preserved.

Verification: `production/qa/evidence/machine-processing-evidence.md`.

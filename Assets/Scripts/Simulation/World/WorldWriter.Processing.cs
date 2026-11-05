using System;

namespace Equilibrium.Simulation.World
{
    public sealed partial class WorldWriter
    {
        private ProcessingSystem _processing;
        internal void BindProcessing(ProcessingSystem processing)
        {
            if (_processing != null) throw new InvalidOperationException("Only one processing system may own this world.");
            _processing = processing;
        }
        private void ApplyProcessing(in Transaction transaction)
        {
            if (_processing == null || !_processing.CanCommit(in transaction, _s.MutationRevision))
                throw new InvalidOperationException("Processing plan is stale or logistics did not commit.");
            for (int g = 0; g < _s.SlotItem.Count; g++)
            {
                _s.SlotItem[g] = _processing.Items[g]; _s.SlotCountArr[g] = _processing.Counts[g]; _s.SlotReserved[g] = _processing.Reserved[g];
            }
            foreach (var pair in _processing.Bags) _s.Bags[pair.Key] = pair.Value;
            _s.MutationRevision++; _processing.DidCommit();
        }

        public ReasonCode CancelMachine(BlockRef block)
        {
            if (!_view.IsAlive(block)) return ReasonCode.TargetInvalid;
            var state = _s.Bags[block.Index].Machine;
            if (state == null || state.Phase != MachinePhase.Working) return ReasonCode.MachineNotAccepting;
            if (_s.Registry.TryGetRecipe(state.RecipeId, out var recipe) && recipe.Irreversible) return ReasonCode.Irreversible;
            Touch();
            foreach (var held in state.Held)
            {
                int g = _view.SlotGlobal(block, held.LocalSlot);
                if (_s.SlotItem[g] == held.Item) _s.SlotReserved[g] = Math.Max(0, _s.SlotReserved[g] - held.Count);
            }
            state.Held.Clear(); state.WorkDone = 0; state.Phase = MachinePhase.Idle; state.Reason = ReasonCode.None;
            _s.Bags[block.Index].Progress = 0; return ReasonCode.None;
        }

        public ReasonCode PinRecipe(BlockRef block, string id)
        {
            if (!_view.IsAlive(block) || _processing == null || !_processing.TryMachine(block, out var machine)) return ReasonCode.TargetInvalid;
            var state = _s.Bags[block.Index].Machine;
            if (state != null && (state.Phase == MachinePhase.Working || state.Phase == MachinePhase.Blocked)) return ReasonCode.MachineNotAccepting;
            bool valid = id == null;
            foreach (var recipe in _s.Registry.RecipesFor(machine)) if (recipe.Id == id) valid = true;
            if (!valid) return ReasonCode.NoRecipe;
            Touch(); state = state ?? new MachineState(); state.PinnedRecipe = id; _s.Bags[block.Index].Machine = state;
            return ReasonCode.None;
        }
    }
}

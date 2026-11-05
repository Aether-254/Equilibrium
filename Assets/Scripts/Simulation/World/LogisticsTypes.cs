using System;
using Equilibrium.Content;

namespace Equilibrium.Simulation.World
{
    public enum AccessorFlow : byte { Auto, BeltToContainer, ContainerToBelt }
    public enum AccessorQuantity : byte { Exact, UpTo, Keep }
    public enum TransferKind : byte { Belt, DirectPort, AccessorDeposit, AccessorWithdraw }

    /// <summary>Immutable block attribute. A null filter means the first available slot's item.</summary>
    public sealed class AccessorConfiguration
    {
        public AccessorFlow Flow { get; }
        public AccessorQuantity Quantity { get; }
        public int Amount { get; }
        public bool AmountClamped { get; }
        public ItemId Filter { get; }
        public string ContainerKey { get; }

        public AccessorConfiguration(AccessorFlow flow = AccessorFlow.Auto,
            AccessorQuantity quantity = AccessorQuantity.UpTo, int amount = 16,
            ItemId? filter = null, string containerKey = "storage")
        {
            if (!Enum.IsDefined(typeof(AccessorFlow), flow)) throw new ArgumentOutOfRangeException(nameof(flow));
            if (!Enum.IsDefined(typeof(AccessorQuantity), quantity)) throw new ArgumentOutOfRangeException(nameof(quantity));
            Flow = flow; Quantity = quantity; Amount = Math.Max(1, Math.Min(3000, amount));
            AmountClamped = Amount != amount; Filter = filter ?? ItemId.None;
            ContainerKey = containerKey ?? throw new ArgumentNullException(nameof(containerKey));
        }
    }

    public readonly struct LogisticsTransfer
    {
        public readonly BlockRef Source, Target;
        public readonly ItemId Item;
        public readonly int Count;
        public readonly TransferKind Kind;
        public LogisticsTransfer(BlockRef source, BlockRef target, ItemId item, int count, TransferKind kind)
        { Source = source; Target = target; Item = item; Count = count; Kind = kind; }
    }

    public readonly struct BeltTickResult
    {
        public readonly int OriginalCount, MovedOut;
        public readonly ReasonCode Reason;
        public bool Stalled => OriginalCount > MovedOut;
        public BeltTickResult(int originalCount, int movedOut, ReasonCode reason)
        { OriginalCount = originalCount; MovedOut = movedOut; Reason = reason; }
    }

    public readonly struct AccessorTickResult
    {
        public readonly AccessorFlow Flow;
        public readonly int Transferred;
        public readonly ReasonCode Reason;
        public AccessorTickResult(AccessorFlow flow, int transferred, ReasonCode reason)
        { Flow = flow; Transferred = transferred; Reason = reason; }
    }

    /// <summary>Machine systems supply current-tick admission reasons; ordinary containers allow access.</summary>
    public interface ILogisticsPortPolicy
    {
        ReasonCode Check(WorldView world, BlockRef block, int port, ItemId item, bool receiving);
    }
}

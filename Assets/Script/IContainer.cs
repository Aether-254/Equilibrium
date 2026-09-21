using System.Collections.Generic;

public interface IContainer
{
    IReadOnlyList<ItemStack> Items { get; }

    int Count { get; }

    int Capacity { get; }

    int SpaceLeft { get; }

    bool IsEmpty { get; }

    bool IsFull { get; }

    bool CanAdd(ItemDef item, int count = 1);

    int Add(ItemDef item, int count = 1);

    int Remove(ItemDef item, int count = 1);

    int CountOf(ItemDef item);
}
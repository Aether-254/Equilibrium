using System;
using System.Collections.Generic;
using System.Linq;

[Serializable]
public class ItemContainer : IContainer
{
    private readonly List<ItemStack> items = new();

    public IReadOnlyList<ItemStack> Items => items;

    public int Capacity { get; }

    public int Count => items.Sum(x => x.Count);

    public int SpaceLeft => Capacity - Count;

    public bool IsEmpty => Count == 0;

    public bool IsFull => Count >= Capacity;


    public ItemContainer(int capacity)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        Capacity = capacity;
    }


    public int CountOf(ItemDef item)
    {
        if (item == null)
            return 0;

        return items
            .Where(x => x.Item == item)
            .Sum(x => x.Count);
    }


    public bool CanAdd(ItemDef item, int count = 1)
    {
        if (item == null || count <= 0)
            return false;

        return SpaceLeft >= count;
    }


    /// <summary>
    /// 添加物品。
    /// 返回实际成功加入的数量。
    /// </summary>
    public int Add(ItemDef item, int count = 1)
    {
        if (item == null || count <= 0)
            return 0;

        int remaining = Math.Min(count, SpaceLeft);
        int addedTotal = 0;


        // 先填已有 Stack
        foreach (ItemStack stack in items)
        {
            if (remaining <= 0)
                break;

            if (stack.Item != item || stack.IsFull)
                continue;

            int added = stack.Add(remaining);

            remaining -= added;
            addedTotal += added;
        }


        // 再创建新 Stack
        while (remaining > 0)
        {
            int stackAmount = Math.Min(
                remaining,
                item.MaxStack
            );

            items.Add(
                new ItemStack(item, stackAmount)
            );

            remaining -= stackAmount;
            addedTotal += stackAmount;
        }


        return addedTotal;
    }


    /// <summary>
    /// 移除指定物品。
    /// 返回实际移除数量。
    /// </summary>
    public int Remove(ItemDef item, int count = 1)
    {
        if (item == null || count <= 0)
            return 0;

        int remaining = count;
        int removedTotal = 0;


        // 倒序删除，方便顺手清理空 Stack
        for (int i = items.Count - 1; i >= 0; i--)
        {
            ItemStack stack = items[i];

            if (stack.Item != item)
                continue;

            int removed = stack.Remove(remaining);

            remaining -= removed;
            removedTotal += removed;

            if (stack.IsEmpty)
                items.RemoveAt(i);

            if (remaining <= 0)
                break;
        }


        return removedTotal;
    }
}
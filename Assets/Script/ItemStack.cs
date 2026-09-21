using System;

[Serializable]
public class ItemStack
{
    public ItemDef Item { get; private set; }

    public int Count { get; private set; }

    public int SpaceLeft => Item.MaxStack - Count;

    public bool IsFull => Count >= Item.MaxStack;

    public bool IsEmpty => Count <= 0;

    public ItemStack(ItemDef item, int count)
    {
        if (item == null)
            throw new ArgumentNullException(nameof(item));

        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));

        Item = item;
        Count = Math.Min(count, item.MaxStack);
    }

    public bool CanMerge(ItemStack other)
    {
        return other != null &&
               other.Item == Item &&
               !IsFull;
    }

    /// <summary>
    /// 尝试向当前 Stack 添加物品。
    /// 返回实际加入的数量。
    /// </summary>
    public int Add(int amount)
    {
        if (amount <= 0)
            return 0;

        int added = Math.Min(amount, SpaceLeft);

        Count += added;

        return added;
    }

    /// <summary>
    /// 尝试从当前 Stack 取出物品。
    /// 返回实际取出的数量。
    /// </summary>
    public int Remove(int amount)
    {
        if (amount <= 0)
            return 0;

        int removed = Math.Min(amount, Count);

        Count -= removed;

        return removed;
    }
}
public static class ContainerTransfer
{
    public static int Transfer(
        IContainer from,
        IContainer to,
        ItemDef item,
        int amount = 1
    )
    {
        if (from == null ||
            to == null ||
            item == null ||
            amount <= 0)
        {
            return 0;
        }

        int available = from.CountOf(item);

        if (available <= 0)
            return 0;

        int transferAmount = System.Math.Min(
            amount,
            available
        );

        transferAmount = System.Math.Min(
            transferAmount,
            to.SpaceLeft
        );

        if (transferAmount <= 0)
            return 0;


        int removed = from.Remove(
            item,
            transferAmount
        );

        int added = to.Add(
            item,
            removed
        );


        // 理论上当前实现不会发生，
        // 但避免数据凭空消失。
        if (added < removed)
        {
            from.Add(
                item,
                removed - added
            );
        }


        return added;
    }
}
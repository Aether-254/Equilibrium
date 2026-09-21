using UnityEngine;

public class ItemTransferLink : MonoBehaviour
{
    [SerializeField]
    private MonoBehaviour sourceBehaviour;

    [SerializeField]
    private MonoBehaviour targetBehaviour;

    [SerializeField]
    private float interval = 0.25f;

    [SerializeField]
    private int amountPerTransfer = 1;


    private IItemContainerOwner Source =>
        sourceBehaviour as IItemContainerOwner;

    private IItemContainerOwner Target =>
        targetBehaviour as IItemContainerOwner;


    private float timer;


    private void Awake()
    {
        if (Source == null)
        {
            Debug.LogError(
                $"{name}: Source must implement IItemContainerOwner."
            );
        }

        if (Target == null)
        {
            Debug.LogError(
                $"{name}: Target must implement IItemContainerOwner."
            );
        }
    }


    private void Update()
    {
        if (Source == null || Target == null)
            return;

        timer += Time.deltaTime;

        while (timer >= interval)
        {
            timer -= interval;

            Transfer();
        }
    }


    private void Transfer()
    {
        ItemContainer source = Source.Container;
        ItemContainer target = Target.Container;

        if (source.IsEmpty)
            return;

        ItemStack stack = source.Items[0];

        ContainerTransfer.Transfer(
            source,
            target,
            stack.Item,
            amountPerTransfer
        );
    }
}
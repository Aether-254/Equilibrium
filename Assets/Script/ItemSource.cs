using UnityEngine;

public class ItemSource : MonoBehaviour, IItemContainerOwner
{
    [SerializeField] private ItemDef outputItem;

    [Min(0.01f)]
    [SerializeField] private float interval = 1f;

    [Min(1)]
    [SerializeField] private int amountPerCycle = 1;

    [Min(1)]
    [SerializeField] private int capacity = 100;


    public ItemContainer Container { get; private set; }


    private float timer;


    private void Awake()
    {
        Container = new ItemContainer(capacity);
    }


    private void Update()
    {
        if (outputItem == null)
            return;

        timer += Time.deltaTime;

        while (timer >= interval)
        {
            timer -= interval;

            Produce();
        }
    }


    private void Produce()
    {
        int added = Container.Add(
            outputItem,
            amountPerCycle
        );

        if (added > 0)
        {
            Debug.Log(
                $"{name} produced {added} {outputItem.DisplayName}. " +
                $"Total: {Container.Count}"
            );
        }
    }
}
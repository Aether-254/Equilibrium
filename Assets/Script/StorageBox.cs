using UnityEngine;

public class StorageBox : MonoBehaviour, IItemContainerOwner
{
    [SerializeField]
    private int capacity = 1000;

    public ItemContainer Container { get; private set; }

    private void Awake()
    {
        Container = new ItemContainer(capacity);
    }
}
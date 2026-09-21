using UnityEngine;

[CreateAssetMenu(
    fileName = "New Item",
    menuName = "Equilibrium/Item Definition"
)]
public class ItemDef : ScriptableObject
{
    [SerializeField] private string id;
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;

    [Min(1)]
    [SerializeField] private int maxStack = 100;

    public string Id => id;
    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public int MaxStack => maxStack;
}
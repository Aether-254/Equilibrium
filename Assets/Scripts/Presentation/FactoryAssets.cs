using UnityEngine;

namespace Equilibrium.Presentation
{
    [CreateAssetMenu(menuName = "Equilibrium/Factory Presentation", fileName = "FactoryPresentation")]
    public sealed class FactoryAssets : ScriptableObject
    {
        public Sprite Belt;
        public Sprite Chest;
        public Sprite Wood;
        public Font WorldFont;
        [Min(0)] public int StartingBelts = 128;
        [Min(0)] public int StartingChests = 16;
        [Min(0)] public int StartingAccessors = 32;
        [Min(0)] public int StartingBlockers = 32;
        [Min(0)] public int StartingWood = 256;
        [Min(0)] public int StartingLogs = 128;
        [Min(0)] public int StartingSaws = 4;
        [Min(0)] public int StartingEngines = 4;
        [Min(0)] public int StartingShafts = 32;
    }
}

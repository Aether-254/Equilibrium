using System;
using System.Collections.Generic;

namespace Equilibrium.Simulation.World
{
    public enum BlockKind : byte { Generic, Container, Machine, Conveyor }
    /// <summary>One container in a block definition: keyed slots with capacity and optional filter.</summary>
    public sealed class ContainerSpec
    {
        public readonly string Key;
        public readonly List<SlotSpec> Slots = new List<SlotSpec>();
        public ContainerSpec(string key) { Key = key; }
        public ContainerSpec Slot(string key, int capacity, int filter = -1) { Slots.Add(new SlotSpec(key, capacity, filter)); return this; }
    }

    public readonly struct SlotSpec
    {
        public readonly string Key;
        public readonly int Capacity;
        public readonly int Filter;
        public SlotSpec(string key, int capacity, int filter) { Key = key; Capacity = capacity; Filter = filter; }
    }

    /// <summary>
    /// Runtime block definition: the item it drops as, its containers/slots/ports. Content (ADR-0003)
    /// authors these; this table is the resolved form the world instantiates from. Ports bind to
    /// containers/slots by string key (container GDD rule 6); binding is verified at instantiation.
    /// </summary>
    public sealed class BlockDefinition
    {
        public string Id { get; }
        public int DropItemHandle { get; private set; } = -1;
        public readonly List<ContainerSpec> Containers = new List<ContainerSpec>();
        public readonly List<PortDef> Ports = new List<PortDef>();
        public bool IsUnknownBlock { get; private set; }
        public BlockKind Kind { get; private set; }

        public BlockDefinition(string id) { Id = id; }

        public ContainerSpec Container(string key) { var c = new ContainerSpec(key); Containers.Add(c); return c; }
        public BlockDefinition Port(PortKind kind, Side facing, string containerKey, string slotKey = null, bool acceptsDirectBelt = false)
        {
            Ports.Add(new PortDef(kind, facing, containerKey, slotKey, acceptsDirectBelt));
            return this;
        }

        /// <summary>grid-world GDD rule 11 default machine layout.</summary>
        public static BlockDefinition Machine(string id, int dropItem, int inputCap, int outputCap, int byproductCap = 0)
        {
            var d = new BlockDefinition(id) { DropItemHandle = dropItem, Kind = BlockKind.Machine };
            d.Container("input").Slot("in_0", inputCap);
            d.Container("output").Slot("out_0", outputCap);
            if (byproductCap > 0) d.Container("byproduct").Slot("by_0", byproductCap);
            d.Port(PortKind.ItemIn, Side.West, "input", acceptsDirectBelt: true);
            d.Port(PortKind.ItemOut, Side.East, "output", acceptsDirectBelt: true);
            d.Port(PortKind.RotationIn, Side.North, null);
            if (byproductCap > 0) d.Port(PortKind.ByproductOut, Side.South, "byproduct", acceptsDirectBelt: true);
            return d;
        }

        /// <summary>Plain container (container GDD rule 7): no belt ports at all.</summary>
        public static BlockDefinition Chest(string id, int dropItem, int slots, int capacity)
        {
            var d = new BlockDefinition(id) { DropItemHandle = dropItem, Kind = BlockKind.Container };
            var c = d.Container("storage");
            for (int i = 0; i < slots; i++) c.Slot("s" + i, capacity);
            return d;
        }

        /// <summary>One group slot; inputs behind and on both sides, with one output ahead.</summary>
        public static BlockDefinition Belt(string id, int dropItem)
        {
            var d = new BlockDefinition(id) { DropItemHandle = dropItem, Kind = BlockKind.Conveyor };
            d.Container("belt").Slot("cell", 3000);
            d.Port(PortKind.ItemIn, Side.West, "belt", acceptsDirectBelt: true);
            d.Port(PortKind.ItemOut, Side.East, "belt", acceptsDirectBelt: true);
            d.Port(PortKind.ItemIn, Side.North, "belt", acceptsDirectBelt: true);
            d.Port(PortKind.ItemIn, Side.South, "belt", acceptsDirectBelt: true);
            return d;
        }

        public static BlockDefinition FuelEngine(string id, int dropItem)
        {
            var d = new BlockDefinition(id) { DropItemHandle = dropItem, Kind = BlockKind.Machine };
            d.Container("fuel").Slot("fuel_0", 64);
            d.Port(PortKind.ItemIn, Side.West, "fuel", acceptsDirectBelt: true);
            d.Port(PortKind.RotationOut, Side.South, null);
            return d;
        }

        /// <summary>Straight, bidirectional shaft. No inferred junctions or gear ratios.</summary>
        public static BlockDefinition Shaft(string id, int dropItem)
        {
            var d = new BlockDefinition(id) { DropItemHandle = dropItem, Kind = BlockKind.Generic };
            d.Port(PortKind.RotationIn, Side.North, null).Port(PortKind.RotationOut, Side.North, null);
            d.Port(PortKind.RotationIn, Side.South, null).Port(PortKind.RotationOut, Side.South, null);
            return d;
        }
    }

    public sealed class BlockDefTable
    {
        private readonly List<BlockDefinition> _defs = new List<BlockDefinition>();
        private readonly Dictionary<string, int> _byId = new Dictionary<string, int>(StringComparer.Ordinal);

        public BlockDefHandle Register(BlockDefinition def)
        {
            if (_byId.ContainsKey(def.Id)) throw new ArgumentException($"duplicate block def {def.Id}");
            _defs.Add(def);
            _byId[def.Id] = _defs.Count - 1;
            return new BlockDefHandle(_defs.Count - 1);
        }

        public bool TryGet(string id, out BlockDefHandle h)
        {
            if (_byId.TryGetValue(id, out int i)) { h = new BlockDefHandle(i); return true; }
            h = BlockDefHandle.None; return false;
        }

        public BlockDefinition Get(BlockDefHandle h) => _defs[h.Index];
        public int Count => _defs.Count;
    }
}

using Godot;
using System.Collections.Generic;

public partial class ContainerInventory : Node, IItemStorage
{
    [ExportCategory("Scene Container Starting Items")]

    // This is for world chests / placed containers that exist as nodes in the scene.
    [Export] public Godot.Collections.Array<Item> StartingItems = new Godot.Collections.Array<Item>();

    // Actual item instances currently inside this container.
    public List<ItemInstance> Items = new List<ItemInstance>();

    // If this inventory was created from an item container resource,
    // this stores its rules: slot count, allowed items, starting items.
    private ItemContainerData _containerData;

    private bool _wasInitializedFromData = false;

    [Signal]
    public delegate void ContainerChangedEventHandler();

    [Signal]
    public delegate void ItemAddedEventHandler(ItemInstance item);

    [Signal]
    public delegate void ItemRemovedEventHandler(ItemInstance item);

    public override void _Ready()
    {
        // This runs only for ContainerInventory nodes placed in the scene,
        // such as world chests.
        //
        // Item containers like matchboxes are initialized manually through
        // InitializeFromData(), so they do not need scene StartingItems.
        if (!_wasInitializedFromData)
            AddSceneStartingItems();
    }

    // This is used by item containers.
    // Example: ItemInstance creates a ContainerInventory for a matchbox,
    // then initializes it from the matchbox ItemContainerData.
    public void InitializeFromData(ItemContainerData containerData)
    {
        _wasInitializedFromData = true;
        _containerData = containerData;

        Items.Clear();

        if (_containerData == null)
        {
            GD.PrintErr("ContainerInventory: InitializeFromData received null container data.");
            EmitSignal(SignalName.ContainerChanged);
            return;
        }

        AddDataStartingItems();

        EmitSignal(SignalName.ContainerChanged);

        GD.Print($"ContainerInventory: Initialized from ItemContainerData. Count: {Items.Count}");
    }

    private void AddSceneStartingItems()
    {
        if (StartingItems == null)
            return;

        for (int i = 0; i < StartingItems.Count; i++)
        {
            Item itemDefinition = StartingItems[i];

            if (itemDefinition == null)
                continue;

            AddItem(itemDefinition);
        }

        GD.Print($"ContainerInventory: Added scene starting items. Count: {Items.Count}");
    }

    private void AddDataStartingItems()
    {
        if (_containerData == null)
            return;

        if (_containerData.StartingItems == null)
            return;

        for (int i = 0; i < _containerData.StartingItems.Count; i++)
        {
            Item itemDefinition = _containerData.StartingItems[i];

            if (itemDefinition == null)
                continue;

            if (Items.Count >= _containerData.SlotCount)
            {
                GD.Print($"ContainerInventory: Slot limit reached. Stopped adding starting items at {Items.Count}.");
                break;
            }

            if (!_containerData.CanAcceptItem(itemDefinition))
            {
                GD.PrintErr($"ContainerInventory: Starting item '{itemDefinition.ItemName}' is not allowed in this container.");
                continue;
            }

            AddItem(itemDefinition);
        }
    }

    // Creates a new ItemInstance from an Item resource and adds it.
    public ItemInstance AddItem(Item itemDefinition)
    {
        if (itemDefinition == null)
            return null;

        ItemInstance itemInstance = new ItemInstance(itemDefinition);

        bool added = AddItemInstance(itemInstance);

        if (!added)
            return null;

        return itemInstance;
    }

    // IItemStorage requirement.
    public bool CanAcceptItemInstance(ItemInstance itemInstance)
    {
        if (itemInstance == null)
            return false;

        if (itemInstance.Definition == null)
            return false;

        // If no container data exists, accept anything.
        // This keeps old chest behavior.
        if (_containerData == null)
            return true;

        if (Items.Count >= _containerData.SlotCount)
            return false;

        return _containerData.CanAcceptItem(itemInstance.Definition);
    }

    // IItemStorage requirement.
    public bool AddItemInstance(ItemInstance itemInstance)
    {
        if (itemInstance == null)
            return false;

        if (itemInstance.Definition == null)
            return false;

        if (!CanAcceptItemInstance(itemInstance))
        {
            GD.Print($"ContainerInventory: Rejected item: {itemInstance.Definition.ItemName}");
            return false;
        }

        Items.Add(itemInstance);

        GD.Print($"ContainerInventory: Added item: {itemInstance.Definition.ItemName}");

        EmitSignal(SignalName.ItemAdded, itemInstance);
        EmitSignal(SignalName.ContainerChanged);

        return true;
    }

    // IItemStorage requirement.
    public bool RemoveItemInstance(ItemInstance itemInstance)
    {
        if (itemInstance == null)
            return false;

        bool removed = Items.Remove(itemInstance);

        if (!removed)
            return false;

        GD.Print($"ContainerInventory: Removed item: {itemInstance.Definition?.ItemName}");

        EmitSignal(SignalName.ItemRemoved, itemInstance);
        EmitSignal(SignalName.ContainerChanged);

        return true;
    }

    // IItemStorage requirement.
    public IReadOnlyList<ItemInstance> GetItems()
    {
        return Items;
    }

    public int GetItemCount()
    {
        return Items.Count;
    }
}
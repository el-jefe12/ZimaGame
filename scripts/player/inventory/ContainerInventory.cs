using Godot;
using System.Collections.Generic;

public partial class ContainerInventory : Node
{
    [Export]
    public Godot.Collections.Array<Item> StartingItems = new Godot.Collections.Array<Item>();

    public List<ItemInstance> Items = new List<ItemInstance>();

    [Signal]
    public delegate void ContainerChangedEventHandler();

    [Signal]
    public delegate void ItemAddedEventHandler(ItemInstance item);

    [Signal]
    public delegate void ItemRemovedEventHandler(ItemInstance item);

    public override void _Ready()
    {
        AddStartingItems();
    }

    private void AddStartingItems()
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

        GD.Print($"ContainerInventory: Added starting items. Count: {Items.Count}");
    }

    public ItemInstance AddItem(Item itemDefinition)
    {
        if (itemDefinition == null)
            return null;

        ItemInstance itemInstance = new ItemInstance(itemDefinition);
        AddItemInstance(itemInstance);

        return itemInstance;
    }

    public bool AddItemInstance(ItemInstance itemInstance)
    {
        if (itemInstance == null)
            return false;

        if (itemInstance.Definition == null)
            return false;

        Items.Add(itemInstance);

        GD.Print($"ContainerInventory: Added item: {itemInstance.Definition.ItemName}");

        EmitSignal(SignalName.ItemAdded, itemInstance);
        EmitSignal(SignalName.ContainerChanged);

        return true;
    }

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

    public IReadOnlyList<ItemInstance> GetItems()
    {
        return Items;
    }

    public int GetItemCount()
    {
        return Items.Count;
    }
}
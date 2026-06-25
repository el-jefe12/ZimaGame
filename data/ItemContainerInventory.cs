using Godot;
using Godot.Collections;

public partial class ItemContainerInventory : RefCounted
{
    public ItemContainerData Data;

    // Actual item instances currently inside this container.
    public Array<ItemInstance> Items = new Array<ItemInstance>();

    public ItemContainerInventory(ItemContainerData data)
    {
        Data = data;

        CreateStartingItems();
    }

    private void CreateStartingItems()
    {
        if (Data == null)
            return;

        foreach (Item startingItem in Data.StartingItems)
        {
            if (startingItem == null)
                continue;

            if (Items.Count >= Data.SlotCount)
                break;

            if (!Data.CanAcceptItem(startingItem))
            {
                GD.PrintErr($"Container starting item {startingItem.ItemName} is not allowed here.");
                continue;
            }

            ItemInstance newInstance = new ItemInstance(startingItem);
            Items.Add(newInstance);
        }
    }

    public bool CanStoreItem(ItemInstance itemInstance)
    {
        if (itemInstance == null)
            return false;

        if (itemInstance.Definition == null)
            return false;

        if (Data == null)
            return false;

        if (Items.Count >= Data.SlotCount)
            return false;

        return Data.CanAcceptItem(itemInstance.Definition);
    }

    public bool TryStoreItem(ItemInstance itemInstance)
    {
        if (!CanStoreItem(itemInstance))
            return false;

        Items.Add(itemInstance);
        return true;
    }

    public bool TryTakeItem(ItemInstance itemInstance)
    {
        if (itemInstance == null)
            return false;

        return Items.Remove(itemInstance);
    }
}
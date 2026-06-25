using Godot;
using Godot.Collections;

[GlobalClass]
public partial class ItemContainerData : Resource
{
    [ExportCategory("Container Settings")]

    // Maximum number of item instances this container can hold.
    [Export] public int SlotCount = 1;

    // Empty = accepts anything.
    // Not empty = only these item resources are allowed.
    [Export] public Array<Item> AllowedItems = new Array<Item>();

    [ExportCategory("Starting Contents")]

    // Add one entry per spawned item.
    // Example: 5 matches = Match, Match, Match, Match, Match.
    [Export] public Array<Item> StartingItems = new Array<Item>();

    public bool CanAcceptItem(Item item)
    {
        if (item == null)
            return false;

        if (AllowedItems == null || AllowedItems.Count == 0)
            return true;

        foreach (Item allowedItem in AllowedItems)
        {
            if (allowedItem == null)
                continue;

            // Same loaded resource instance.
            if (allowedItem == item)
                return true;

            // Same .tres path.
            if (!string.IsNullOrEmpty(allowedItem.ResourcePath) &&
                !string.IsNullOrEmpty(item.ResourcePath) &&
                allowedItem.ResourcePath == item.ResourcePath)
            {
                return true;
            }
        }

        return false;
    }
}
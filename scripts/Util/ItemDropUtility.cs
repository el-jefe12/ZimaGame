using Godot;

public static class ItemDropUtility
{
    public static bool TryDropFromStorage(
        Node requester,
        IItemStorage sourceStorage,
        ItemInstance item
    )
    {
        if (requester == null)
            return false;

        if (sourceStorage == null)
            return false;

        if (item == null || item.Definition == null)
            return false;

        WorldItemDropper dropper =
            requester.GetTree().GetFirstNodeInGroup("world_item_dropper") as WorldItemDropper;

        if (dropper == null)
        {
            GD.PushError("ItemDropUtility: No WorldItemDropper found in group 'world_item_dropper'.");
            return false;
        }

        bool dropped = dropper.DropItemInstanceFromCamera(item);

        if (!dropped)
        {
            GD.Print($"ItemDropUtility: Drop failed. Item stays in source: {item.Definition.ItemName}");
            return false;
        }

        bool removed = sourceStorage.RemoveItemInstance(item);

        if (!removed)
        {
            GD.Print($"ItemDropUtility: Dropped item but failed to remove from source: {item.Definition.ItemName}");
            return false;
        }

        GD.Print($"ItemDropUtility: Dropped item from storage: {item.Definition.ItemName}");

        return true;
    }
}
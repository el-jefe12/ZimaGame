using Godot;

public static class ItemDragReleaseUtility
{
    public static bool TryDropDraggedItem(
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

        return ItemDropUtility.TryDropFromStorage(
            requester,
            sourceStorage,
            item
        );
    }
}
using System.Collections.Generic;

public sealed class HotbarStorageAdapter : IItemStorage
{
    private readonly HotBarScript _hotbar;

    public HotbarStorageAdapter(HotBarScript hotbar)
    {
        _hotbar = hotbar;
    }

    public IReadOnlyList<ItemInstance> GetItems()
    {
        if (_hotbar == null)
            return new List<ItemInstance>();

        return _hotbar.GetHotbarItems();
    }

    public bool CanAcceptItemInstance(ItemInstance itemInstance)
    {
        if (_hotbar == null)
            return false;

        if (itemInstance == null || itemInstance.Definition == null)
            return false;

        return _hotbar.HasFreeSlot();
    }

    public bool AddItemInstance(ItemInstance itemInstance)
    {
        if (_hotbar == null)
            return false;

        return _hotbar.TryAddItemToFirstFreeSlot(itemInstance);
    }

    public bool RemoveItemInstance(ItemInstance itemInstance)
    {
        if (_hotbar == null)
            return false;

        return _hotbar.TryRemoveItemInstance(itemInstance);
    }
}
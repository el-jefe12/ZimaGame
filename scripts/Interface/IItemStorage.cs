using System.Collections.Generic;

public interface IItemStorage
{
    IReadOnlyList<ItemInstance> GetItems();

    bool CanAcceptItemInstance(ItemInstance itemInstance);

    bool AddItemInstance(ItemInstance itemInstance);

    bool RemoveItemInstance(ItemInstance itemInstance);
}
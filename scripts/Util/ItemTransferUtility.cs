using Godot;

public static class ItemTransferUtility
{
	public static bool TryMoveItem(
		IItemStorage source,
		IItemStorage target,
		ItemInstance itemInstance
	)
	{
		if (source == null)
		{
			GD.PrintErr("ItemTransferUtility: Source storage is null.");
			return false;
		}

		if (target == null)
		{
			GD.PrintErr("ItemTransferUtility: Target storage is null.");
			return false;
		}

		if (itemInstance == null || itemInstance.Definition == null)
		{
			GD.PrintErr("ItemTransferUtility: Item is null or has no definition.");
			return false;
		}

		if (!target.CanAcceptItemInstance(itemInstance))
		{
			GD.Print($"ItemTransferUtility: Target rejected item: {itemInstance.Definition.ItemName}");
			return false;
		}

		bool removedFromSource = source.RemoveItemInstance(itemInstance);

		if (!removedFromSource)
		{
			GD.Print($"ItemTransferUtility: Could not remove item from source: {itemInstance.Definition.ItemName}");
			return false;
		}

		bool addedToTarget = target.AddItemInstance(itemInstance);

		if (!addedToTarget)
		{
			// Rollback if possible.
			source.AddItemInstance(itemInstance);

			GD.Print($"ItemTransferUtility: Could not add item to target, rollback: {itemInstance.Definition.ItemName}");
			return false;
		}

		GD.Print($"ItemTransferUtility: Moved item: {itemInstance.Definition.ItemName}");

		return true;
	}
}
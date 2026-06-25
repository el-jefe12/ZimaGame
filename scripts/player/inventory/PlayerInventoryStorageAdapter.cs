using System.Collections.Generic;

public sealed class PlayerInventoryStorageAdapter : IItemStorage
{
	private readonly PlayerInventory _playerInventory;

	public PlayerInventoryStorageAdapter(PlayerInventory playerInventory)
	{
		_playerInventory = playerInventory;
	}

	public IReadOnlyList<ItemInstance> GetItems()
	{
		if (_playerInventory == null)
			return new List<ItemInstance>();

		return _playerInventory.playerInventory;
	}

	public bool CanAcceptItemInstance(ItemInstance itemInstance)
	{
		if (_playerInventory == null)
			return false;

		if (itemInstance == null)
			return false;

		if (itemInstance.Definition == null)
			return false;

		// Later you can add weight/slot limits here.
		return true;
	}

	public bool AddItemInstance(ItemInstance itemInstance)
	{
		if (!CanAcceptItemInstance(itemInstance))
			return false;

		// Your current PlayerInventory.AddItemInstance returns void.
		_playerInventory.AddItemInstance(itemInstance);

		return true;
	}

	public bool RemoveItemInstance(ItemInstance itemInstance)
	{
		if (_playerInventory == null)
			return false;

		if (itemInstance == null)
			return false;

		return _playerInventory.RemoveItemInstance(itemInstance);
	}
}
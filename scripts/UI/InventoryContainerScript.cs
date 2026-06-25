using Godot;

public partial class InventoryContainerScript : InventoryPanelBase
{
    [ExportCategory("Player Inventory Panel")]

    // You can keep using your old direct slot template export.
    [Export] public InventoryItemSlot SlotTemplate;

    [ExportCategory("Groups")]
    [Export] public string PlayerGroupName = "player";
    [Export] public string HotbarGroupName = "hotbar";
    [Export] public string InventoryContainerGroupName = "inventory_container";
    [Export] public string ContainerPanelGroupName = "container_inventory_panel";

    private PlayerInventory _inventory;
    private PlayerInventoryStorageAdapter _playerStorage;

    public override void _Ready()
    {
        GD.Print("InventoryContainer: Ready");

        AddToGroup(InventoryContainerGroupName);

        // If SlotTemplate is assigned, it uses that.
        // If not, it falls back to SlotTemplatePath from the base class.
        SetupSharedPanel(SlotTemplate);

        FindPlayerInventory();

        if (_inventory == null)
        {
            GD.PushError("InventoryContainer: PlayerInventory was not found.");
            return;
        }

        _playerStorage = new PlayerInventoryStorageAdapter(_inventory);

        _inventory.ItemAdded += OnInventoryItemAdded;
        _inventory.ItemRemoved += OnInventoryItemRemoved;
        _inventory.InventoryChanged += OnInventoryChanged;

        RebuildInventoryFromList();
    }

    public override void _ExitTree()
    {
        CleanupManualDrag();

        if (_inventory == null)
            return;

        _inventory.ItemAdded -= OnInventoryItemAdded;
        _inventory.ItemRemoved -= OnInventoryItemRemoved;
        _inventory.InventoryChanged -= OnInventoryChanged;
    }

    private void FindPlayerInventory()
    {
        _inventory = null;

        Node player = GetTree().GetFirstNodeInGroup(PlayerGroupName);

        if (player == null)
        {
            GD.PushError($"InventoryContainer: No player found in group '{PlayerGroupName}'.");
            return;
        }

        _inventory = player.GetNodeOrNull<PlayerInventory>("%Inventory");

        if (_inventory != null)
            return;

        _inventory = player.GetNodeOrNull<PlayerInventory>("Inventory");

        if (_inventory != null)
            return;

        _inventory = player.FindChild("Inventory", true, false) as PlayerInventory;
    }

    private void OnInventoryItemAdded(ItemInstance item)
    {
        RebuildInventoryFromList();
    }

    private void OnInventoryItemRemoved(ItemInstance item)
    {
        RebuildInventoryFromList();
    }

    private void OnInventoryChanged()
    {
        RebuildInventoryFromList();
    }

    public void RebuildInventoryFromList()
    {
        if (_inventory == null)
            return;

        RebuildFromItems(_inventory.playerInventory, "InventoryItemSlot");
    }

    protected override void OnSlotRemoveRequested(ItemInstance item)
    {
        // Inventory is no longer allowed to throw/drop items.
        // Only hotbar discard can throw items into the world.
        GD.Print("InventoryContainer: Drop/remove from inventory is disabled. Move item to hotbar and discard from hotbar.");
    }

    protected override void OnSlotActionRequested(ItemInstance item)
    {
        ExecuteItemAction(item);
    }

    protected override void OnSlotDragRequested(ItemInstance item)
    {
        StartManualDrag(item);
    }

    protected override void FinishManualDrag()
    {
        if (!ManualDragActive)
            return;

        ItemInstance draggedItem = ManualDragItem;

        if (draggedItem == null || draggedItem.Definition == null)
        {
            CleanupManualDrag();
            return;
        }

        // Player inventory -> container.
        ContainerInventoryPanel containerPanel =
            GetTree().GetFirstNodeInGroup(ContainerPanelGroupName) as ContainerInventoryPanel;

        if (containerPanel != null)
        {
            bool movedToContainer = containerPanel.TryAcceptPlayerInventoryItem(draggedItem);

            if (movedToContainer)
            {
                GD.Print($"InventoryContainer: Moved item to container: {draggedItem.Definition.ItemName}");
                CleanupManualDrag();
                return;
            }
        }

        // Player inventory -> hotbar.
        HotBarScript hotbar = GetTree().GetFirstNodeInGroup(HotbarGroupName) as HotBarScript;

        if (hotbar != null)
        {
            bool movedToHotbar = hotbar.TryAcceptItemAtMouseFromStorage(
                draggedItem,
                _playerStorage
            );

            if (movedToHotbar)
            {
                GD.Print($"InventoryContainer: Moved item to hotbar: {draggedItem.Definition.ItemName}");
                CleanupManualDrag();
                return;
            }
        }

		bool dropped = ItemDropUtility.TryDropFromStorage(
			this,
			_playerStorage,
			draggedItem
		);

		if (!dropped)
			GD.Print("InventoryContainer: Drag released outside valid target. Drop failed, item stays in inventory.");

		CleanupManualDrag();
    }

    public bool TryAcceptHotbarItemFromStorage(ItemInstance item, IItemStorage hotbarStorage)
    {
        if (_playerStorage == null)
            return false;

        if (hotbarStorage == null)
            return false;

        if (item == null || item.Definition == null)
            return false;

        if (!IsMouseOverInventory())
            return false;

        bool moved = ItemTransferUtility.TryMoveItem(
            hotbarStorage,
            _playerStorage,
            item
        );

        if (!moved)
        {
            GD.Print($"InventoryContainer: Could not move hotbar item to inventory: {item.Definition.ItemName}");
            return false;
        }

        GD.Print($"InventoryContainer: Moved hotbar item to inventory: {item.Definition.ItemName}");

        return true;
    }

    // Old compatibility method. New hotbar code should not use this.
    public bool TryAcceptHotbarItem(ItemInstance item)
    {
        if (_inventory == null)
            return false;

        if (item == null || item.Definition == null)
            return false;

        if (!IsMouseOverInventory())
            return false;

        GD.Print($"InventoryContainer: Accepting item from hotbar: {item.Definition.ItemName}");

        _inventory.AddItemInstance(item);

        return true;
    }

    public bool IsMouseOverInventory()
    {
        return IsMouseOverPanel();
    }

    private void ExecuteItemAction(ItemInstance item)
    {
        if (_inventory == null)
            return;

        if (item == null || item.Definition == null)
            return;

        GD.Print($"InventoryContainer: Action executed: {item.Definition.ItemName}");

        if (item.Definition.ConsumableData != null)
        {
            GD.Print($"InventoryContainer: Consumed item removed from inventory: {item.Definition.ItemName}");
            _inventory.RemoveItemInstance(item);
            return;
        }

        RebuildInventoryFromList();
    }
}
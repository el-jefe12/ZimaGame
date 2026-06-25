using Godot;

public partial class ContainerInventoryPanel : InventoryPanelBase
{
    [ExportCategory("Container Panel")]
    [Export] public string PlayerGroupName = "player";
    [Export] public string HotbarGroupName = "hotbar";
    [Export] public string InventoryContainerGroupName = "inventory_container";
    [Export] public string ContainerPanelGroupName = "container_inventory_panel";

    private ContainerInventory _containerInventory;
    private PlayerInventory _playerInventory;
    private PlayerInventoryStorageAdapter _playerStorage;

    private HotBarScript _hotbar;

    // If this panel was opened by a held item, this tracks the item that must stay selected.
    private ItemInstance _requiredHeldItem;
    private bool _closeWhenHeldItemChanges = false;

    public override void _Ready()
    {
        GD.Print("ContainerInventoryPanel: Ready");

        AddToGroup(ContainerPanelGroupName);

        SetupSharedPanel();
        FindPlayerInventory();

        if (_playerInventory == null)
        {
            GD.PushError("ContainerInventoryPanel: PlayerInventory was not found.");
            return;
        }

        _playerStorage = new PlayerInventoryStorageAdapter(_playerInventory);

        Visible = false;
    }

    public override void _ExitTree()
    {
        CleanupManualDrag();
        DisconnectContainerSignals();
        DisconnectHotbarSignal();
    }

    private void FindPlayerInventory()
    {
        _playerInventory = null;

        Node player = GetTree().GetFirstNodeInGroup(PlayerGroupName);

        if (player == null)
        {
            GD.PushError($"ContainerInventoryPanel: No player found in group '{PlayerGroupName}'.");
            return;
        }

        _playerInventory = player.GetNodeOrNull<PlayerInventory>("%Inventory");

        if (_playerInventory != null)
            return;

        _playerInventory = player.GetNodeOrNull<PlayerInventory>("Inventory");

        if (_playerInventory != null)
            return;

        _playerInventory = player.FindChild("Inventory", true, false) as PlayerInventory;
    }

    // Use this for normal world containers/chests.
    public void OpenContainer(ContainerInventory containerInventory)
    {
        OpenContainer(containerInventory, null, false);
    }

    // Use this for containers opened from a held hotbar item.
    public void OpenContainer(
        ContainerInventory containerInventory,
        ItemInstance requiredHeldItem,
        bool closeWhenHeldItemChanges
    )
    {
        DisconnectContainerSignals();
        DisconnectHotbarSignal();

        _containerInventory = containerInventory;
        _requiredHeldItem = requiredHeldItem;
        _closeWhenHeldItemChanges = closeWhenHeldItemChanges;

        if (_containerInventory == null)
        {
            GD.PushError("ContainerInventoryPanel: Tried to open a null container.");
            return;
        }

        _containerInventory.ItemAdded += OnContainerItemAdded;
        _containerInventory.ItemRemoved += OnContainerItemRemoved;
        _containerInventory.ContainerChanged += OnContainerChanged;

        if (_closeWhenHeldItemChanges)
        {
            _hotbar = GetTree().GetFirstNodeInGroup(HotbarGroupName) as HotBarScript;

            if (_hotbar == null)
            {
                GD.PushError("ContainerInventoryPanel: Could not find hotbar for held-item container tracking.");
                CloseContainer();
                return;
            }

            _hotbar.SelectedItemChanged += OnHotbarSelectedItemChanged;

            if (!IsRequiredHeldItemStillSelected())
            {
                CloseContainer();
                return;
            }
        }

        Visible = true;

        RebuildContainerFromList();

        GD.Print("ContainerInventoryPanel: Opened container.");
    }

    public void CloseContainer()
    {
        CleanupManualDrag();
        DisconnectContainerSignals();
        DisconnectHotbarSignal();

        _containerInventory = null;
        _requiredHeldItem = null;
        _closeWhenHeldItemChanges = false;

        ClearGrid();

        Visible = false;

        GD.Print("ContainerInventoryPanel: Closed container.");
    }

    private void DisconnectContainerSignals()
    {
        if (_containerInventory == null)
            return;

        _containerInventory.ItemAdded -= OnContainerItemAdded;
        _containerInventory.ItemRemoved -= OnContainerItemRemoved;
        _containerInventory.ContainerChanged -= OnContainerChanged;
    }

    private void DisconnectHotbarSignal()
    {
        if (_hotbar == null)
            return;

        _hotbar.SelectedItemChanged -= OnHotbarSelectedItemChanged;
        _hotbar = null;
    }

    private void OnHotbarSelectedItemChanged(ItemInstance selectedItem)
    {
        if (!_closeWhenHeldItemChanges)
            return;

        if (selectedItem == _requiredHeldItem)
            return;

        GD.Print("ContainerInventoryPanel: Closing because the opening item is no longer selected.");

        CloseContainer();
    }

    private bool IsRequiredHeldItemStillSelected()
    {
        if (!_closeWhenHeldItemChanges)
            return true;

        if (_requiredHeldItem == null)
            return false;

        if (_hotbar == null)
            return false;

        return _hotbar.GetSelectedItem() == _requiredHeldItem;
    }

    private void OnContainerItemAdded(ItemInstance item)
    {
        RebuildContainerFromList();
    }

    private void OnContainerItemRemoved(ItemInstance item)
    {
        RebuildContainerFromList();
    }

    private void OnContainerChanged()
    {
        RebuildContainerFromList();
    }

    public void RebuildContainerFromList()
    {
        if (_containerInventory == null)
            return;

        RebuildFromItems(_containerInventory.GetItems(), "ContainerItemSlot");
    }

    protected override void OnSlotRemoveRequested(ItemInstance item)
    {
        TakeItemFromContainer(item);
    }

    protected override void OnSlotActionRequested(ItemInstance item)
    {
        TakeItemFromContainer(item);
    }

    protected override void OnSlotDragRequested(ItemInstance item)
    {
        StartManualDrag(item);
    }

    private void TakeItemFromContainer(ItemInstance item)
    {
        if (_containerInventory == null)
            return;

        if (_playerStorage == null)
            return;

        if (item == null || item.Definition == null)
            return;

        bool moved = ItemTransferUtility.TryMoveItem(
            _containerInventory,
            _playerStorage,
            item
        );

        if (!moved)
        {
            GD.Print($"ContainerInventoryPanel: Could not take item from container: {item.Definition.ItemName}");
            return;
        }

        GD.Print($"ContainerInventoryPanel: Took item from container: {item.Definition.ItemName}");
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

        // Container -> hotbar.
        HotBarScript hotbar = GetTree().GetFirstNodeInGroup(HotbarGroupName) as HotBarScript;

        if (hotbar != null)
        {
            bool movedToHotbar = hotbar.TryAcceptItemAtMouseFromStorage(
                draggedItem,
                _containerInventory
            );

            if (movedToHotbar)
            {
                GD.Print($"ContainerInventoryPanel: Moved container item to hotbar: {draggedItem.Definition.ItemName}");
                CleanupManualDrag();
                return;
            }
        }

        // Container -> player inventory.
        InventoryContainerScript playerInventoryPanel =
            GetTree().GetFirstNodeInGroup(InventoryContainerGroupName) as InventoryContainerScript;

        if (playerInventoryPanel != null && playerInventoryPanel.IsMouseOverInventory())
        {
            TakeItemFromContainer(draggedItem);
            CleanupManualDrag();
            return;
        }

        // Container -> world.
        bool dropped = ItemDropUtility.TryDropFromStorage(
            this,
            _containerInventory,
            draggedItem
        );

        if (!dropped)
            GD.Print("ContainerInventoryPanel: Drag released outside valid target. Drop failed, item stays in container.");

        CleanupManualDrag();
    }

    public bool TryAcceptPlayerInventoryItem(ItemInstance item)
    {
        if (_containerInventory == null)
            return false;

        if (_playerStorage == null)
            return false;

        if (item == null || item.Definition == null)
            return false;

        if (!IsMouseOverPanel())
            return false;

        bool moved = ItemTransferUtility.TryMoveItem(
            _playerStorage,
            _containerInventory,
            item
        );

        if (!moved)
        {
            GD.Print($"ContainerInventoryPanel: Could not move player item to container: {item.Definition.ItemName}");
            return false;
        }

        GD.Print($"ContainerInventoryPanel: Moved player item to container: {item.Definition.ItemName}");

        return true;
    }

    public bool TryAcceptHotbarItemFromStorage(ItemInstance item, IItemStorage hotbarStorage)
    {
        if (_containerInventory == null)
            return false;

        if (hotbarStorage == null)
            return false;

        if (item == null || item.Definition == null)
            return false;

        if (!IsMouseOverPanel())
            return false;

        bool moved = ItemTransferUtility.TryMoveItem(
            hotbarStorage,
            _containerInventory,
            item
        );

        if (!moved)
        {
            GD.Print($"ContainerInventoryPanel: Could not move hotbar item to container: {item.Definition.ItemName}");
            return false;
        }

        GD.Print($"ContainerInventoryPanel: Moved hotbar item to container: {item.Definition.ItemName}");

        return true;
    }

    public bool IsMouseOverContainer()
    {
        return IsMouseOverPanel();
    }
}
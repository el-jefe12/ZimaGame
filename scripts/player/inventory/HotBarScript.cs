using Godot;
using System;
using System.Collections.Generic;

public partial class HotBarScript : PanelContainer
{
    public event Action<ItemInstance> SelectedItemChanged;

    private WeaponHolder _weaponHolder;
    private playerHUD _playerHUD;

    private buttonActionIndicatorLogic _buttonActionIndicatorLogic;

    private TextureRect _selectedItemSlot;
    private TextureRect[] _itemSlotArray;

    private ItemInstance _lastEquippedItem;
    private RichTextLabel _CurrentItemNameRichTextLabel;

    [ExportCategory("Groups")]
    [Export] public string InventoryContainerGroupName = "inventory_container";
    [Export] public string ContainerPanelGroupName = "container_inventory_panel";
    [Export] public string HotbarGroupName = "hotbar";

    private readonly ItemInstance[] _hotbarItems = new ItemInstance[5];

    private int _selectedIndex = 0;

    private PlayerInventory _inventory;
    private HotbarStorageAdapter _hotbarStorage;

    private bool _manualDragActive = false;
    private int _manualDragSourceSlot = -1;
    private ItemInstance _manualDragItem = null;
    private CanvasLayer _manualDragLayer = null;
    private TextureRect _manualDragPreview = null;

    private bool _holdUseActive = false;
    private bool _holdUseCompleted = false;
    private float _holdUseTimer = 0.0f;
    private ItemInstance _holdUseItem = null;
    private ItemAction _holdUseAction = null;

    public override void _Ready()
    {
        GD.Print("HotBar: Ready");

        AddToGroup(HotbarGroupName);

        _hotbarStorage = new HotbarStorageAdapter(this);

        // Cache UI nodes first, before any slot visual updates.
        _selectedItemSlot = GetNodeOrNull<TextureRect>("%SelectedItemSlot");
        _CurrentItemNameRichTextLabel = GetNodeOrNull<RichTextLabel>("%CurrentItemNameRichTextLabel");

        _itemSlotArray = new TextureRect[]
        {
            GetNodeOrNull<TextureRect>("%ItemSlot0"),
            GetNodeOrNull<TextureRect>("%ItemSlot1"),
            GetNodeOrNull<TextureRect>("%ItemSlot2"),
            GetNodeOrNull<TextureRect>("%ItemSlot3"),
            GetNodeOrNull<TextureRect>("%ItemSlot4"),
        };

        MouseFilter = MouseFilterEnum.Ignore;

        if (_selectedItemSlot != null)
            _selectedItemSlot.MouseFilter = MouseFilterEnum.Ignore;

        for (int i = 0; i < _itemSlotArray.Length; i++)
        {
            if (_itemSlotArray[i] != null)
                _itemSlotArray[i].MouseFilter = MouseFilterEnum.Ignore;
            else
                GD.PushError($"HotBar: ItemSlot{i} was not found. Check the node unique name.");
        }

        Node player = GetTree().GetFirstNodeInGroup("player");

        if (player != null)
        {
            _weaponHolder = GetTree().GetFirstNodeInGroup("weapon_holder") as WeaponHolder;

            if (_weaponHolder == null)
                GD.PushError("HotBar: WeaponHolder not found under player.");

            _inventory = player.GetNodeOrNull<PlayerInventory>("%Inventory");

            if (_inventory == null)
                _inventory = player.GetNodeOrNull<PlayerInventory>("Inventory");

            if (_inventory != null)
            {
                GD.Print("HotBar: Connected to PlayerInventory");

                _inventory.HotbarItemAdded += OnHotbarItemAdded;

                for (int i = 0; i < _inventory.playerHotBar.Length && i < _hotbarItems.Length; i++)
                {
                    _hotbarItems[i] = _inventory.playerHotBar[i];
                    UpdateSlotVisual(i);
                }
            }
            else
            {
                GD.PushError("HotBar: PlayerInventory not found.");
            }
        }
        else
        {
            GD.PushError("HotBar: Player not found.");
        }

        _playerHUD = GetTree().GetFirstNodeInGroup("hud") as playerHUD;

        if (_playerHUD == null)
            GD.PushError("HotBar: PlayerHUD not found.");

        UpdateSelectionVisual();
        UpdateSelectedItemNameText();
        PrintHotbarState();

        CallDeferred(nameof(UpdateHeldItem));
        CallDeferred(nameof(UpdateHotbarActionPrompt));
        CallDeferred(nameof(EmitSelectedItemChanged));
    }

    public override void _ExitTree()
    {
        if (_inventory != null)
            _inventory.HotbarItemAdded -= OnHotbarItemAdded;

        HideHotbarActionPrompts();
        CleanupManualHotbarDrag();
    }

    public override void _Process(double delta)
    {
        if (_manualDragActive)
            UpdateManualDragPreviewPosition();

        if (IsInventoryMode() || IsItemUIMode())
        {
            ClearHoldUse();
            return;
        }

        UpdateHoldUse((float)delta);
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (robinsonGlobals.Instance != null && robinsonGlobals.Instance.OpenInventory)
        {
            HandleInventoryOpenMouseInput(inputEvent);
            return;
        }

        if (_manualDragActive)
        {
            CleanupManualHotbarDrag();
            return;
        }

        if (!IsVisibleInTree())
            return;

        HandleActionInput(inputEvent);

        if (inputEvent.IsActionPressed("game_scroll_up"))
        {
            GD.Print("HotBar: Scroll Up");
            ChangeSelection(-1);
        }

        if (inputEvent.IsActionPressed("game_scroll_down"))
        {
            GD.Print("HotBar: Scroll Down");
            ChangeSelection(1);
        }

        if (inputEvent.IsActionPressed("game_item_discard"))
        {
            GD.Print("HotBar: Drop key pressed");
            DropSelectedItem();
        }
    }

    private void HandleInventoryOpenMouseInput(InputEvent inputEvent)
    {
        if (!IsVisibleInTree())
            return;

        if (inputEvent is not InputEventMouseButton mouseButton)
            return;

        if (mouseButton.ButtonIndex != MouseButton.Left)
            return;

        if (mouseButton.Pressed)
        {
            if (_manualDragActive)
                return;

            int sourceSlot = GetSlotIndexAtGlobalMousePosition();

            GD.Print($"HotBar: Left click while inventory open. Slot under mouse: {sourceSlot}");

            if (sourceSlot < 0 || sourceSlot >= _hotbarItems.Length)
                return;

            ItemInstance item = _hotbarItems[sourceSlot];

            if (item == null || item.Definition == null)
            {
                GD.Print("HotBar: Clicked hotbar slot is empty.");
                return;
            }

            StartManualHotbarDrag(sourceSlot, item);
            GetViewport().SetInputAsHandled();
            return;
        }

        if (!mouseButton.Pressed)
        {
            if (!_manualDragActive)
                return;

            FinishManualHotbarDrag();
            GetViewport().SetInputAsHandled();
        }
    }

    private void StartManualHotbarDrag(int sourceSlot, ItemInstance item)
    {
        ClearHoldUse();

        if (item == null || item.Definition == null)
            return;

        GD.Print($"HotBar: Manual drag started from slot {sourceSlot} item {item.Definition.ItemName}");

        _manualDragActive = true;
        _manualDragSourceSlot = sourceSlot;
        _manualDragItem = item;

        if (_manualDragLayer != null)
            _manualDragLayer.QueueFree();

        _manualDragLayer = new CanvasLayer();
        _manualDragLayer.Layer = 1000;
        GetTree().Root.AddChild(_manualDragLayer);

        _manualDragPreview = new TextureRect();
        _manualDragPreview.Texture = item.Definition.Icon;
        _manualDragPreview.Size = new Vector2(56.0f, 56.0f);
        _manualDragPreview.CustomMinimumSize = new Vector2(56.0f, 56.0f);
        _manualDragPreview.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        _manualDragPreview.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        _manualDragPreview.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.75f);
        _manualDragPreview.MouseFilter = MouseFilterEnum.Ignore;

        _manualDragLayer.AddChild(_manualDragPreview);

        UpdateManualDragPreviewPosition();
    }

    private void FinishManualHotbarDrag()
    {
        if (!_manualDragActive)
            return;

        int sourceSlot = _manualDragSourceSlot;
        int targetSlot = GetSlotIndexAtGlobalMousePosition();

        GD.Print($"HotBar: Manual drag released. Source slot: {sourceSlot}, target slot: {targetSlot}");

        if (sourceSlot < 0 || sourceSlot >= _hotbarItems.Length)
        {
            CleanupManualHotbarDrag();
            return;
        }

        ItemInstance item = _hotbarItems[sourceSlot];

        if (item == null || item.Definition == null)
        {
            CleanupManualHotbarDrag();
            return;
        }

        // Hotbar -> hotbar slot.
        if (targetSlot >= 0 && targetSlot < _hotbarItems.Length)
        {
            if (targetSlot != sourceSlot)
                MoveOrSwapHotbarSlots(sourceSlot, targetSlot);
            else
                GD.Print("HotBar: Released on original slot. No change.");

            CleanupManualHotbarDrag();
            return;
        }

        // Hotbar -> open container panel.
        ContainerInventoryPanel containerPanel =
            GetTree().GetFirstNodeInGroup(ContainerPanelGroupName) as ContainerInventoryPanel;

        if (containerPanel != null)
        {
            bool movedToContainer = containerPanel.TryAcceptHotbarItemFromStorage(item, _hotbarStorage);

            if (movedToContainer)
            {
                GD.Print($"HotBar: Moved item from hotbar to container: {item.Definition.ItemName}");
                CleanupManualHotbarDrag();
                return;
            }
        }

        // Hotbar -> player inventory panel.
        InventoryContainerScript inventoryContainer =
            GetTree().GetFirstNodeInGroup(InventoryContainerGroupName) as InventoryContainerScript;

        if (inventoryContainer != null)
        {
            bool movedToInventory = inventoryContainer.TryAcceptHotbarItemFromStorage(item, _hotbarStorage);

            if (movedToInventory)
            {
                GD.Print($"HotBar: Moved item from hotbar to inventory: {item.Definition.ItemName}");
                CleanupManualHotbarDrag();
                return;
            }
        }

        // Hotbar -> world.
        bool dropped = ItemDropUtility.TryDropFromStorage(
            this,
            _hotbarStorage,
            item
        );

        if (!dropped)
            GD.Print("HotBar: Released outside valid target. Drop failed, item stays in hotbar.");

        CleanupManualHotbarDrag();
    }

    public bool TryAcceptItemAtMouseFromStorage(ItemInstance item, IItemStorage sourceStorage)
    {
        if (sourceStorage == null)
            return false;

        if (item == null || item.Definition == null)
            return false;

        int targetSlot = GetSlotIndexAtGlobalMousePosition();

        if (targetSlot < 0 || targetSlot >= _hotbarItems.Length)
            return false;

        if (_hotbarItems[targetSlot] != null)
        {
            GD.Print($"HotBar: Cannot accept item. Slot {targetSlot} is occupied.");
            return false;
        }

        bool removedFromSource = sourceStorage.RemoveItemInstance(item);

        if (!removedFromSource)
        {
            GD.Print($"HotBar: Could not remove item from source: {item.Definition.ItemName}");
            return false;
        }

        PlaceItemInSlot(targetSlot, item);

        GD.Print($"HotBar: Accepted item '{item.Definition.ItemName}' into slot {targetSlot}");

        return true;
    }

    public bool TryAcceptInventoryItemAtMouse(ItemInstance item)
    {
        if (item == null || item.Definition == null)
            return false;

        int targetSlot = GetSlotIndexAtGlobalMousePosition();

        if (targetSlot < 0 || targetSlot >= _hotbarItems.Length)
            return false;

        if (_hotbarItems[targetSlot] != null)
        {
            GD.Print($"HotBar: Cannot accept inventory item. Slot {targetSlot} is occupied.");
            return false;
        }

        PlaceItemInSlot(targetSlot, item);

        GD.Print($"HotBar: Accepted inventory item '{item.Definition.ItemName}' into slot {targetSlot}");

        return true;
    }

    public IReadOnlyList<ItemInstance> GetHotbarItems()
    {
        return _hotbarItems;
    }

    public bool HasFreeSlot()
    {
        for (int i = 0; i < _hotbarItems.Length; i++)
        {
            if (_hotbarItems[i] == null)
                return true;
        }

        return false;
    }

    public bool TryAddItemToFirstFreeSlot(ItemInstance item)
    {
        if (item == null || item.Definition == null)
            return false;

        for (int i = 0; i < _hotbarItems.Length; i++)
        {
            if (_hotbarItems[i] != null)
                continue;

            PlaceItemInSlot(i, item);

            GD.Print($"HotBar: Added item to first free slot {i}: {item.Definition.ItemName}");

            return true;
        }

        return false;
    }

    public bool TryRemoveItemInstance(ItemInstance item)
    {
        if (item == null)
            return false;

        for (int i = 0; i < _hotbarItems.Length; i++)
        {
            if (_hotbarItems[i] != item)
                continue;

            RemoveItem(i, false);
            return true;
        }

        return false;
    }

    private void PlaceItemInSlot(int slot, ItemInstance item)
    {
        if (slot < 0 || slot >= _hotbarItems.Length)
            return;

        _hotbarItems[slot] = item;

        RefreshHotbarAfterSlotChange(slot);
    }

    private void MoveOrSwapHotbarSlots(int sourceSlot, int targetSlot)
    {
        if (sourceSlot < 0 || sourceSlot >= _hotbarItems.Length)
            return;

        if (targetSlot < 0 || targetSlot >= _hotbarItems.Length)
            return;

        ItemInstance sourceItem = _hotbarItems[sourceSlot];

        if (sourceItem == null || sourceItem.Definition == null)
            return;

        ItemInstance targetItem = _hotbarItems[targetSlot];

        GD.Print($"HotBar: Moving '{sourceItem.Definition.ItemName}' from slot {sourceSlot} to slot {targetSlot}");

        _hotbarItems[targetSlot] = sourceItem;
        _hotbarItems[sourceSlot] = targetItem;

        RefreshHotbarAfterSlotChange(sourceSlot);
        RefreshHotbarAfterSlotChange(targetSlot);
    }

    public void RemoveItem(int slot)
    {
        RemoveItem(slot, false);
    }

    public void RemoveItem(int slot, bool playHideAnimation)
    {
        ClearHoldUse();

        GD.Print($"HotBar: Removing item from slot {slot}");

        if (slot < 0 || slot >= _hotbarItems.Length)
            return;

        bool removedSelectedHeldItem = slot == _selectedIndex && _hotbarItems[slot] != null;

        _hotbarItems[slot] = null;

        SyncInventorySlot(slot);
        UpdateSlotVisual(slot);
        UpdateSelectionVisual();
        UpdateSelectedItemNameText();

        if (removedSelectedHeldItem && _weaponHolder != null)
        {
            _lastEquippedItem = null;
            _weaponHolder.ClearItem(playHideAnimation);
        }
        else
        {
            UpdateHeldItem();
        }

        UpdateHotbarActionPrompt();
        PrintHotbarState();

        if (slot == _selectedIndex)
            EmitSelectedItemChanged();
    }

    private void RefreshHotbarAfterSlotChange(int slot)
    {
        SyncInventorySlot(slot);
        UpdateSlotVisual(slot);
        UpdateSelectionVisual();
        UpdateSelectedItemNameText();
        UpdateHeldItem();
        UpdateHotbarActionPrompt();
        PrintHotbarState();

        if (slot == _selectedIndex)
            EmitSelectedItemChanged();
    }

    private void SyncInventorySlot(int slot)
    {
        if (_inventory == null)
            return;

        if (_inventory.playerHotBar == null)
            return;

        if (slot < 0 || slot >= _inventory.playerHotBar.Length)
            return;

        _inventory.playerHotBar[slot] = _hotbarItems[slot];
    }

    private void OnHotbarItemAdded(ItemInstance item, int slot)
    {
        if (item == null || item.Definition == null)
            return;

        if (slot < 0 || slot >= _hotbarItems.Length)
            return;

        GD.Print($"HotBar: Received item '{item.Definition.ItemName}' for slot {slot}");

        PlaceItemInSlot(slot, item);
    }

    private void CleanupManualHotbarDrag()
    {
        _manualDragActive = false;
        _manualDragSourceSlot = -1;
        _manualDragItem = null;

        if (_manualDragLayer != null)
        {
            _manualDragLayer.QueueFree();
            _manualDragLayer = null;
        }

        _manualDragPreview = null;
    }

    private void UpdateManualDragPreviewPosition()
    {
        if (_manualDragPreview == null)
            return;

        Vector2 mousePosition = GetViewport().GetMousePosition();

        _manualDragPreview.GlobalPosition =
            mousePosition - (_manualDragPreview.Size * 0.5f);
    }

    private int GetSlotIndexAtGlobalMousePosition()
    {
        if (_itemSlotArray == null)
            return -1;

        Vector2 mousePosition = GetViewport().GetMousePosition();

        for (int i = 0; i < _itemSlotArray.Length; i++)
        {
            TextureRect slotNode = _itemSlotArray[i];

            if (slotNode == null)
                continue;

            if (slotNode.GetGlobalRect().HasPoint(mousePosition))
                return i;
        }

        return -1;
    }

    public void UpdateSlot(int slot, ItemInstance item)
    {
        if (_itemSlotArray == null)
        {
            GD.PushError("HotBar: Cannot update slot because _itemSlotArray is null.");
            return;
        }

        if (slot < 0 || slot >= _itemSlotArray.Length)
            return;

        TextureRect slotNode = _itemSlotArray[slot];

        if (slotNode == null)
        {
            GD.PushError($"HotBar: Cannot update slot {slot} because ItemSlot{slot} is null.");
            return;
        }

        TextureRect icon = slotNode.GetNodeOrNull<TextureRect>("Icon");

        if (icon == null)
        {
            icon = new TextureRect();
            icon.Name = "Icon";
            icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            icon.MouseFilter = MouseFilterEnum.Ignore;
            icon.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            slotNode.AddChild(icon);
        }

        icon.Position = Vector2.Zero;
        icon.Size = slotNode.Size;

        icon.Texture = item?.Definition.Icon;
        icon.Visible = item != null;
        icon.MouseFilter = MouseFilterEnum.Ignore;
    }

    private void UpdateSlotVisual(int index)
    {
        if (_itemSlotArray == null)
        {
            GD.PushError("HotBar: Cannot update slot visual because _itemSlotArray is null.");
            return;
        }

        if (index < 0 || index >= _itemSlotArray.Length)
            return;

        if (_itemSlotArray[index] == null)
        {
            GD.PushError($"HotBar: Cannot update slot visual {index} because ItemSlot{index} is null.");
            return;
        }

        UpdateSlot(index, _hotbarItems[index]);
    }

    private void UpdateSelectionVisual()
    {
        if (_selectedItemSlot == null)
            return;

        if (_itemSlotArray == null)
            return;

        if (_selectedIndex < 0 || _selectedIndex >= _itemSlotArray.Length)
            return;

        TextureRect selectedSlot = _itemSlotArray[_selectedIndex];

        if (selectedSlot == null)
            return;

        _selectedItemSlot.GlobalPosition = selectedSlot.GlobalPosition;
    }

    private void ChangeSelection(int direction)
    {
        ClearHoldUse();

        int previousIndex = _selectedIndex;

        _selectedIndex += direction;

        if (_itemSlotArray == null || _itemSlotArray.Length == 0)
            return;

        if (_selectedIndex < 0)
            _selectedIndex = _itemSlotArray.Length - 1;

        if (_selectedIndex >= _itemSlotArray.Length)
            _selectedIndex = 0;

        GD.Print($"HotBar: Selection changed {previousIndex} -> {_selectedIndex}");

        UpdateSelectionVisual();
        UpdateSelectedItemNameText();
        UpdateHotbarActionPrompt();
        UpdateHeldItem();

        EmitSelectedItemChanged();
    }

    public ItemInstance GetSelectedItem()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _hotbarItems.Length)
            return null;

        return _hotbarItems[_selectedIndex];
    }

    private void UpdateHeldItem()
    {
        if (_weaponHolder == null)
            return;

        ItemInstance item = GetSelectedItem();

        // Nothing changed, so don't replay equip/holster logic.
        if (_lastEquippedItem == item)
            return;

        ItemInstance previousItem = _lastEquippedItem;

        _lastEquippedItem = item;

        // Selected slot is empty.
        // If we previously had an item equipped, play the hide/holster animation.
        if (item == null)
        {
            bool shouldPlayHideAnimation = previousItem != null;
            _weaponHolder.ClearItem(shouldPlayHideAnimation);
            return;
        }

        // Selected item exists but cannot be shown in hand.
        // Treat this like holstering if something was previously equipped.
        if (item.Definition == null || item.Definition.WorldModel == null)
        {
            bool shouldPlayHideAnimation = previousItem != null;
            _weaponHolder.ClearItem(shouldPlayHideAnimation);
            return;
        }

        // Selected item has a valid hand model, so equip it.
        _weaponHolder.EquipItem(item);
    }

    private void HandleActionInput(InputEvent inputEvent)
    {
        if (IsInventoryMode() || IsItemUIMode())
            return;

        ItemInstance item = GetSelectedItem();

        if (item == null || item.Definition == null)
            return;

        // Do not allow the selected item's action to run unless the correct model
        // is actually equipped visually.
        if (_weaponHolder != null && !_weaponHolder.IsReadyForItem(item))
            return;

        if (item.Definition.Actions == null || item.Definition.Actions.Count == 0)
            return;

        Node player = GetTree().GetFirstNodeInGroup("player");

        if (player == null)
            return;

        for (int i = 0; i < item.Definition.Actions.Count; i++)
        {
            ItemAction action = item.Definition.Actions[i];

            if (action == null)
                continue;

            if (string.IsNullOrWhiteSpace(action.InputActionName))
                continue;

            if (inputEvent.IsActionPressed(action.InputActionName))
            {
                if (!action.CanExecute(player, item))
                    continue;

                if (action.HoldRequired)
                {
                    StartHoldUse(item, action);
                    return;
                }

                ExecuteAction(item, action);
                return;
            }

            if (inputEvent.IsActionReleased(action.InputActionName))
            {
                if (_holdUseActive && _holdUseAction == action)
                {
                    ClearHoldUse();
                    return;
                }
            }
        }
    }

    private void StartHoldUse(ItemInstance item, ItemAction action)
    {
        if (item == null || item.Definition == null || action == null)
            return;

        _holdUseActive = true;
        _holdUseCompleted = false;
        _holdUseTimer = 0.0f;
        _holdUseItem = item;
        _holdUseAction = action;

        GD.Print($"HotBar: Started hold action {action.GetType().Name} for {item.Definition.ItemName}.");
    }

    private void UpdateHoldUse(float delta)
    {
        if (!_holdUseActive)
            return;

        if (_holdUseCompleted)
            return;

        if (_holdUseAction == null || string.IsNullOrWhiteSpace(_holdUseAction.InputActionName))
        {
            ClearHoldUse();
            return;
        }

        if (!Input.IsActionPressed(_holdUseAction.InputActionName))
        {
            ClearHoldUse();
            return;
        }

        if (_holdUseItem == null || _holdUseItem.Definition == null)
        {
            ClearHoldUse();
            return;
        }

        if (GetSelectedItem() != _holdUseItem)
        {
            ClearHoldUse();
            return;
        }

        _holdUseTimer += delta;

        if (_holdUseTimer < _holdUseAction.HoldDuration)
            return;

        _holdUseCompleted = true;

        ExecuteAction(_holdUseItem, _holdUseAction);

        ClearHoldUse();
    }

    private void ClearHoldUse()
    {
        _holdUseActive = false;
        _holdUseCompleted = false;
        _holdUseTimer = 0.0f;
        _holdUseItem = null;
        _holdUseAction = null;
    }

    private void ExecuteAction(ItemInstance item, ItemAction action)
    {
        if (item == null || item.Definition == null || action == null)
            return;

        Node player = GetTree().GetFirstNodeInGroup("player");

        if (player == null)
            return;

        ItemActionResult result = action.Execute(player, item);

        ApplyActionResult(item, result);
    }

    private void ApplyActionResult(ItemInstance item, ItemActionResult result)
    {
        if ((result & ItemActionResult.RemoveItem) != 0)
        {
            RemoveItem(_selectedIndex, false);
            return;
        }

        if ((result & ItemActionResult.RefreshHeldItem) != 0)
            UpdateHeldItem();

        if ((result & ItemActionResult.RefreshPrompt) != 0)
            UpdateHotbarActionPrompt();
    }

    private void DropSelectedItem()
    {
        ClearHoldUse();

        ItemInstance item = GetSelectedItem();

        if (item == null || item.Definition == null)
        {
            GD.Print("HotBar: Drop failed. Selected slot is empty.");
            return;
        }

        bool dropped = ItemDropUtility.TryDropFromStorage(
            this,
            _hotbarStorage,
            item
        );

        if (!dropped)
        {
            GD.Print("HotBar: Drop failed. Item stays in hotbar.");
            return;
        }

        GD.Print($"HotBar: Dropped item into world: {item.Definition.ItemName}");
    }

    private void UpdateHotbarActionPrompt()
    {
        HideHotbarActionPrompts();

        if (_playerHUD == null)
            return;

        ItemInstance item = GetSelectedItem();

        if (item == null || item.Definition == null)
            return;

        if (item.Definition.Actions == null || item.Definition.Actions.Count == 0)
            return;

        Node player = GetTree().GetFirstNodeInGroup("player");

        if (player == null)
            return;

        for (int i = 0; i < item.Definition.Actions.Count; i++)
        {
            ItemAction action = item.Definition.Actions[i];

            if (action == null)
                continue;

            if (!action.ShowInHotbarPrompt)
                continue;

            if (string.IsNullOrWhiteSpace(action.InputActionName))
                continue;

            if (!action.CanExecute(player, item))
                continue;

            string promptId = $"hotbar_action_{i}";
            string text = action.GetInteractionText(item);

            _playerHUD.ShowButtonAction(
                promptId,
                new StringName(action.InputActionName),
                text,
                action.HoldRequired
            );
        }
    }

    private void HideHotbarActionPrompts()
    {
        if (_playerHUD == null)
            return;

        for (int i = 0; i < 20; i++)
            _playerHUD.HideButtonAction($"hotbar_action_{i}");

        _playerHUD.HideButtonAction("hotbar_use");
        _playerHUD.HideButtonAction("selected_item");
        _playerHUD.HideButtonAction("use_item");
        _playerHUD.HideButtonAction("hotbar_item");
    }

    public void UseSelectedItem()
    {
        if (IsInventoryMode() || IsItemUIMode())
            return;

        ItemInstance item = GetSelectedItem();

        if (item == null || item.Definition == null)
            return;

        if (_weaponHolder != null && !_weaponHolder.IsReadyForItem(item))
            return;
            
        if (item.Definition.Actions == null || item.Definition.Actions.Count == 0)
            return;

        Node player = GetTree().GetFirstNodeInGroup("player");

        if (player == null)
            return;

        for (int i = 0; i < item.Definition.Actions.Count; i++)
        {
            ItemAction action = item.Definition.Actions[i];

            if (action == null)
                continue;

            if (!action.ShowInHotbarPrompt)
                continue;

            if (!action.CanExecute(player, item))
                continue;

            ExecuteAction(item, action);
            return;
        }
    }

    private void UpdateSelectedItemNameText()
    {
        if (_CurrentItemNameRichTextLabel == null)
            return;

        ItemInstance item = GetSelectedItem();

        if (item == null || item.Definition == null)
        {
            _CurrentItemNameRichTextLabel.Text = "Empty";
            return;
        }

        _CurrentItemNameRichTextLabel.Text = item.Definition.ItemName;
    }

    private void EmitSelectedItemChanged()
    {
        SelectedItemChanged?.Invoke(GetSelectedItem());
    }

    public void GetSelectedItemName()
    {
        UpdateSelectedItemNameText();
    }

    public int GetSelectedIndex()
    {
        return _selectedIndex;
    }

    private bool IsInventoryMode()
    {
        return robinsonGlobals.Instance != null && robinsonGlobals.Instance.OpenInventory;
    }

    private bool IsItemUIMode()
    {
        return robinsonGlobals.Instance != null && robinsonGlobals.Instance.OpenItemUI;
    }

    private void PrintHotbarState()
    {
        GD.Print("HotBar: ===== HOTBAR STATE =====");

        for (int i = 0; i < _hotbarItems.Length; i++)
        {
            ItemInstance item = _hotbarItems[i];

            if (item == null)
                GD.Print($"Slot {i}: EMPTY");
            else
                GD.Print($"Slot {i}: {item.Definition.ItemName}");
        }

        GD.Print("HotBar: =======================");
    }
}
using Godot;
using System;

public partial class HotBarScript : PanelContainer
{
    // Handles the visible 3D model of the currently selected hotbar item.
    private WeaponHolder _weaponHolder;

    // Gives this script access to HUD prompts.
    private playerHUD _playerHUD;

    // Visual frame/marker that sits on top of the selected hotbar slot.
    private TextureRect _selectedItemSlot;

    // All five hotbar slot UI nodes.
    private TextureRect[] _itemSlotArray;

    // Remembers which item is currently equipped so we do not re-equip it every frame.
    private ItemInstance _lastEquippedItem;

    // RichTextLabel that shows the name of the selected hotbar item.
    private RichTextLabel _CurrentItemNameRichTextLabel;

    // Scene used when throwing/dropping an item into the world.
    [Export]
    public PackedScene ItemScene;

    // Group name used to find the inventory UI container.
    [Export]
    public string InventoryContainerGroupName = "inventory_container";

    // Group name assigned to this hotbar.
    [Export]
    public string HotbarGroupName = "hotbar";

    // Actual item data stored in the hotbar slots.
    private ItemInstance[] _hotbarItems = new ItemInstance[5];

    // Index of the currently selected hotbar slot.
    private int _selectedIndex = 0;

    // Player inventory that owns/syncs the hotbar data.
    private PlayerInventory _inventory;

    // Manual drag state for moving hotbar items while inventory is open.
    private bool _manualDragActive = false;
    private int _manualDragSourceSlot = -1;
    private ItemInstance _manualDragItem = null;
    private CanvasLayer _manualDragLayer = null;
    private TextureRect _manualDragPreview = null;

    // Hold-use state for actions that require holding a button.
    private bool _holdUseActive = false;
    private bool _holdUseCompleted = false;
    private float _holdUseTimer = 0.0f;
    private ItemInstance _holdUseItem = null;
    private ItemAction _holdUseAction = null;

    // Sets up node references, connects inventory events, and draws the starting hotbar.
    public override void _Ready()
    {
        GD.Print("HotBar: Ready");

        Node player = GetTree().GetFirstNodeInGroup("player");

        if (player != null)
        {
            _weaponHolder = GetTree().GetFirstNodeInGroup("weapon_holder") as WeaponHolder;

            if (_weaponHolder == null)
                GD.PushError("HotBar: WeaponHolder not found under player");
        }

        AddToGroup(HotbarGroupName);

        _selectedItemSlot = GetNode<TextureRect>("%SelectedItemSlot");

        _CurrentItemNameRichTextLabel = GetNode<RichTextLabel>("%CurrentItemNameRichTextLabel");

        _itemSlotArray = new TextureRect[]
        {
            GetNode<TextureRect>("%ItemSlot0"),
            GetNode<TextureRect>("%ItemSlot1"),
            GetNode<TextureRect>("%ItemSlot2"),
            GetNode<TextureRect>("%ItemSlot3"),
            GetNode<TextureRect>("%ItemSlot4"),
        };

        MouseFilter = MouseFilterEnum.Ignore;

        if (_selectedItemSlot != null)
            _selectedItemSlot.MouseFilter = MouseFilterEnum.Ignore;

        for (int i = 0; i < _itemSlotArray.Length; i++)
        {
            if (_itemSlotArray[i] != null)
                _itemSlotArray[i].MouseFilter = MouseFilterEnum.Ignore;
        }

        _playerHUD = GetTree().GetFirstNodeInGroup("hud") as playerHUD;

        if (_playerHUD == null)
            GD.PushError("HotBar: PlayerHUD not found");

        if (player != null)
        {
            _inventory = player.GetNodeOrNull<PlayerInventory>("%Inventory");

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
                GD.PushError("HotBar: PlayerInventory not found");
            }
        }
        else
        {
            GD.PushError("HotBar: Player not found");
        }

        UpdateSelectionVisual();
        UpdateSelectedItemNameText();
        PrintHotbarState();

        CallDeferred(nameof(UpdateHeldItem));
        CallDeferred(nameof(UpdateHotbarActionPrompt));
    }

    // Disconnects events and clears HUD prompts when this node is removed.
    public override void _ExitTree()
    {
        if (_inventory != null)
            _inventory.HotbarItemAdded -= OnHotbarItemAdded;

        HideHotbarActionPrompts();
    }

    // Updates drag preview and hold-use timing every frame.
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

    // Handles hotbar input: inventory dragging, item actions, scrolling, and discarding.
    public override void _Input(InputEvent @event)
    {
        if (robinsonGlobals.Instance != null && robinsonGlobals.Instance.OpenInventory)
        {
            HandleInventoryOpenMouseInput(@event);
            return;
        }

        if (_manualDragActive)
        {
            CleanupManualHotbarDrag();
            return;
        }

        if (!IsVisibleInTree())
            return;

        HandleActionInput(@event);

        if (@event.IsActionPressed("game_scroll_up"))
        {
            GD.Print("HotBar: Scroll Up");
            ChangeSelection(-1);
        }

        if (@event.IsActionPressed("game_scroll_down"))
        {
            GD.Print("HotBar: Scroll Down");
            ChangeSelection(1);
        }

        if (@event.IsActionPressed("game_item_discard"))
        {
            GD.Print("HotBar: Discard key pressed");
            DiscardSelectedItem();
        }
    }

    // Checks whether the selected item's actions were triggered by input.
    private void HandleActionInput(InputEvent inputEvent)
    {
        if (IsInventoryMode() || IsItemUIMode())
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

            if (string.IsNullOrWhiteSpace(action.InputActionName))
                continue;

            if (inputEvent.IsActionPressed(action.InputActionName))
            {
                if (!action.CanExecute(player, item))
                    continue;

                GD.Print($"HotBar: Input triggered action {i}: {action.GetType().Name}");

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
                    if (!_holdUseCompleted)
                        GD.Print("HotBar: Hold use cancelled.");

                    ClearHoldUse();
                    return;
                }
            }
        }
    }

    // Starts tracking a hold-required item action.
    private void StartHoldUse(ItemInstance item, ItemAction action)
    {
        if (item == null || item.Definition == null || action == null)
            return;

        _holdUseActive = true;
        _holdUseCompleted = false;
        _holdUseTimer = 0.0f;
        _holdUseItem = item;
        _holdUseAction = action;

        GD.Print(
            $"HotBar: Started hold action {action.GetType().Name} for {item.Definition.ItemName}. " +
            $"Required duration: {action.HoldDuration}"
        );
    }

    // Finishes or cancels a hold-required action.
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

        ItemInstance selectedItem = GetSelectedItem();

        if (selectedItem != _holdUseItem)
        {
            GD.Print("HotBar: Hold use cancelled because selected item changed.");
            ClearHoldUse();
            return;
        }

        _holdUseTimer += delta;

        if (_holdUseTimer < _holdUseAction.HoldDuration)
            return;

        _holdUseCompleted = true;

        GD.Print($"HotBar: Hold action completed for {_holdUseItem.Definition.ItemName}.");

        ExecuteAction(_holdUseItem, _holdUseAction);

        ClearHoldUse();
    }

    // Resets all hold-use state.
    private void ClearHoldUse()
    {
        _holdUseActive = false;
        _holdUseCompleted = false;
        _holdUseTimer = 0.0f;
        _holdUseItem = null;
        _holdUseAction = null;
    }

    // Runs an item action and applies its result.
    private void ExecuteAction(ItemInstance item, ItemAction action)
    {
        if (item == null || item.Definition == null || action == null)
            return;

        Node player = GetTree().GetFirstNodeInGroup("player");

        if (player == null)
        {
            GD.PrintErr("HotBar: ExecuteAction failed. Player not found.");
            return;
        }

        GD.Print($"HotBar: Executing action {action.GetType().Name} for {item.Definition.ItemName}.");

        ItemActionResult result = action.Execute(player, item);

        GD.Print($"HotBar: Action result: {result}");

        ApplyActionResult(item, result);
    }

    // Applies action result flags such as removing the item or refreshing the HUD.
    private void ApplyActionResult(ItemInstance item, ItemActionResult result)
    {
        if ((result & ItemActionResult.RemoveItem) != 0)
        {
            string itemName = item?.Definition?.ItemName ?? "unknown item";

            GD.Print($"HotBar: Action removed item {itemName}");

            RemoveItem(_selectedIndex);
            return;
        }

        if ((result & ItemActionResult.RefreshHeldItem) != 0)
            UpdateHeldItem();

        if ((result & ItemActionResult.RefreshPrompt) != 0)
            UpdateHotbarActionPrompt();
    }

    // Handles clicking and dragging hotbar items while the inventory is open.
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

    // Creates the floating icon preview when dragging a hotbar item.
    private void StartManualHotbarDrag(int sourceSlot, ItemInstance item)
    {
        ClearHoldUse();

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

    // Drops the dragged item into another hotbar slot, inventory, or the world.
    private void FinishManualHotbarDrag()
    {
        if (!_manualDragActive)
            return;

        int sourceSlot = _manualDragSourceSlot;
        int targetSlot = GetSlotIndexAtGlobalMousePosition();

        GD.Print($"HotBar: Manual drag released. Source slot: {sourceSlot}, target slot: {targetSlot}");

        if (targetSlot >= 0 && targetSlot < _hotbarItems.Length)
        {
            if (targetSlot != sourceSlot)
                MoveOrSwapHotbarSlots(sourceSlot, targetSlot);
            else
                GD.Print("HotBar: Released on original slot. No change.");

            CleanupManualHotbarDrag();
            return;
        }

        InventoryContainerScript inventoryContainer =
            GetTree().GetFirstNodeInGroup(InventoryContainerGroupName) as InventoryContainerScript;

        if (inventoryContainer != null)
        {
            ItemInstance item = _hotbarItems[sourceSlot];

            if (item != null && item.Definition != null)
            {
                bool movedToInventory = inventoryContainer.TryAcceptHotbarItem(item);

                if (movedToInventory)
                {
                    GD.Print($"HotBar: Moved item from hotbar to inventory: {item.Definition.ItemName}");

                    if (_weaponHolder != null)
                        _weaponHolder.ClearItem(false);

                    RemoveItem(sourceSlot);

                    CleanupManualHotbarDrag();
                    return;
                }
            }
        }

        GD.Print("HotBar: Released outside hotbar and inventory. Throwing item into world.");
        ThrowDraggedHotbarItem(sourceSlot);

        CleanupManualHotbarDrag();
    }

    // Attempts to place an inventory item into the hotbar slot under the mouse.
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

        GD.Print($"HotBar: Accepting inventory item '{item.Definition.ItemName}' into slot {targetSlot}");

        _hotbarItems[targetSlot] = item;

        SyncInventorySlot(targetSlot);

        UpdateSlotVisual(targetSlot);
        UpdateSelectionVisual();
        UpdateSelectedItemNameText();
        UpdateHotbarActionPrompt();
        UpdateHeldItem();

        PrintHotbarState();

        return true;
    }

    // Throws a dragged hotbar item into the world.
    private void ThrowDraggedHotbarItem(int sourceSlot)
    {
        if (sourceSlot < 0 || sourceSlot >= _hotbarItems.Length)
            return;

        ItemInstance item = _hotbarItems[sourceSlot];

        if (item == null || item.Definition == null)
            return;

        bool spawned = SpawnWorldItem(item);

        if (!spawned)
        {
            GD.Print("HotBar: Drag throw failed. Item stays in hotbar.");
            return;
        }

        GD.Print($"HotBar: Drag-threw item '{item.Definition.ItemName}' into world.");

        if (_weaponHolder != null)
            _weaponHolder.ClearItem(false);

        RemoveItem(sourceSlot);
    }

    // Removes the drag preview and clears drag state.
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

    // Keeps the dragged item preview centered on the mouse.
    private void UpdateManualDragPreviewPosition()
    {
        if (_manualDragPreview == null)
            return;

        Vector2 mousePosition = GetViewport().GetMousePosition();

        _manualDragPreview.GlobalPosition =
            mousePosition - (_manualDragPreview.Size * 0.5f);
    }

    // Returns the hotbar slot index under the mouse, or -1 if none.
    private int GetSlotIndexAtGlobalMousePosition()
    {
        Vector2 mousePosition = GetViewport().GetMousePosition();

        for (int i = 0; i < _itemSlotArray.Length; i++)
        {
            TextureRect slotNode = _itemSlotArray[i];

            if (slotNode == null)
                continue;

            Rect2 globalRect = slotNode.GetGlobalRect();

            if (globalRect.HasPoint(mousePosition))
                return i;
        }

        return -1;
    }

    // Swaps/moves items between two hotbar slots.
    private void MoveOrSwapHotbarSlots(int sourceSlot, int targetSlot)
    {
        if (sourceSlot < 0 || sourceSlot >= _hotbarItems.Length)
            return;

        if (targetSlot < 0 || targetSlot >= _hotbarItems.Length)
            return;

        ItemInstance sourceItem = _hotbarItems[sourceSlot];

        if (sourceItem == null)
            return;

        ItemInstance targetItem = _hotbarItems[targetSlot];

        GD.Print(
            $"HotBar: Moving '{sourceItem.Definition.ItemName}' from slot {sourceSlot} to slot {targetSlot}"
        );

        _hotbarItems[targetSlot] = sourceItem;
        _hotbarItems[sourceSlot] = targetItem;

        SyncInventorySlot(sourceSlot);
        SyncInventorySlot(targetSlot);

        UpdateSlotVisual(sourceSlot);
        UpdateSlotVisual(targetSlot);

        UpdateSelectionVisual();
        UpdateSelectedItemNameText();
        UpdateHotbarActionPrompt();
        UpdateHeldItem();

        PrintHotbarState();
    }

    // Copies one hotbar slot back into the PlayerInventory array.
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

    // Receives new hotbar items from PlayerInventory.
    private void OnHotbarItemAdded(ItemInstance item, int slot)
    {
        if (item == null || item.Definition == null)
            return;

        GD.Print($"HotBar: Received item '{item.Definition.ItemName}' for slot {slot}");

        if (slot < 0 || slot >= _hotbarItems.Length)
            return;

        _hotbarItems[slot] = item;

        SyncInventorySlot(slot);

        UpdateSlotVisual(slot);
        UpdateSelectedItemNameText();
        UpdateHotbarActionPrompt();
        UpdateHeldItem();

        PrintHotbarState();
    }

    // Redraws one hotbar slot icon.
    private void UpdateSlotVisual(int index)
    {
        GD.Print($"HotBar: Updating slot visual {index}");

        if (index < 0 || index >= _itemSlotArray.Length)
            return;

        UpdateSlot(index, _hotbarItems[index]);
    }

    // Moves the selected slot left/right and refreshes related visuals.
    private void ChangeSelection(int direction)
    {
        ClearHoldUse();

        int previousIndex = _selectedIndex;

        _selectedIndex += direction;

        if (_selectedIndex < 0)
            _selectedIndex = _itemSlotArray.Length - 1;

        if (_selectedIndex >= _itemSlotArray.Length)
            _selectedIndex = 0;

        GD.Print($"HotBar: Selection changed {previousIndex} -> {_selectedIndex}");

        PrintSelectedSlotInfo();

        UpdateSelectionVisual();
        UpdateSelectedItemNameText();
        UpdateHotbarActionPrompt();
        UpdateHeldItem();
    }

    // Moves the selected-slot frame to the selected hotbar slot.
    private void UpdateSelectionVisual()
    {
        GD.Print($"HotBar: Updating selection visual for slot {_selectedIndex}");

        if (_selectedItemSlot == null)
            return;

        if (_selectedIndex < 0 || _selectedIndex >= _itemSlotArray.Length)
            return;

        _selectedItemSlot.GlobalPosition =
            _itemSlotArray[_selectedIndex].GlobalPosition;
    }

    // Returns the currently selected hotbar item, or null if the slot is empty.
    public ItemInstance GetSelectedItem()
    {
        GD.Print($"HotBar: GetSelectedItem slot {_selectedIndex}");

        ItemInstance item = _hotbarItems[_selectedIndex];

        if (item == null)
            GD.Print("HotBar: Selected slot empty");
        else
            GD.Print($"HotBar: Selected item {item.Definition.ItemName}");

        return item;
        
    }

    // Equips or clears the 3D held item model based on the selected item.
    private void UpdateHeldItem()
    {
        GD.Print("HotBar: UpdateHeldItem called");

        if (_weaponHolder == null)
            return;

        ItemInstance item = GetSelectedItem();

        if (_lastEquippedItem == item)
            return;

        _lastEquippedItem = item;

        if (item == null)
        {
            _weaponHolder.ClearItem();
            return;
        }

        if (item.Definition.WorldModel == null)
        {
            GD.Print($"HotBar: {item.Definition.ItemName} has no WorldModel");
            _weaponHolder.ClearItem();
            return;
        }

        _weaponHolder.EquipItem(item);
    }

    // Removes an item from a hotbar slot and refreshes UI/state.
    public void RemoveItem(int slot)
    {
        ClearHoldUse();

        GD.Print($"HotBar: Removing item from slot {slot}");

        if (slot < 0 || slot >= _hotbarItems.Length)
            return;

        _hotbarItems[slot] = null;

        SyncInventorySlot(slot);

        UpdateSlotVisual(slot);
        UpdateSelectedItemNameText();
        UpdateHeldItem();
        UpdateHotbarActionPrompt();
        PrintHotbarState();
    }

    // Sets the icon texture/visibility for a hotbar slot.
    public void UpdateSlot(int slot, ItemInstance item)
    {
        GD.Print($"HotBar: UpdateSlot {slot} item {(item != null ? item.Definition.ItemName : "NULL")}");

        if (slot < 0 || slot >= _itemSlotArray.Length)
            return;

        TextureRect slotNode = _itemSlotArray[slot];
        TextureRect icon = slotNode.GetNodeOrNull<TextureRect>("Icon");

        if (icon == null)
        {
            GD.Print($"HotBar: Creating icon node for slot {slot}");

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

    // Throws the selected item into the world and removes it from the hotbar.
    private void DiscardSelectedItem()
    {
        ClearHoldUse();

        GD.Print("HotBar: Attempting to discard selected item");

        PrintSelectedSlotInfo();

        ItemInstance item = GetSelectedItem();

        if (item == null)
        {
            GD.Print("HotBar: Discard failed (slot empty)");
            return;
        }

        bool spawned = SpawnWorldItem(item);

        if (!spawned)
        {
            GD.Print("HotBar: Discard throw failed. Item stays in hotbar.");
            return;
        }

        GD.Print($"HotBar: Item '{item.Definition.ItemName}' thrown into world");

        if (_weaponHolder != null)
            _weaponHolder.ClearItem(false);

        RemoveItem(_selectedIndex);

        UpdateHotbarActionPrompt();
    }

    // Spawns a WorldItem scene in front of the camera.
    private bool SpawnWorldItem(ItemInstance item)
    {
        GD.Print($"HotBar: SpawnWorldItem {item?.Definition?.ItemName}");

        if (item == null)
            return false;

        Camera3D cam = GetViewport().GetCamera3D();

        if (cam == null)
        {
            GD.Print("HotBar: Camera not found");
            return false;
        }

        Vector3 forward = -cam.GlobalTransform.Basis.Z;

        PackedScene worldItemScene = ItemScene;

        if (worldItemScene == null)
        {
            GD.PushError("HotBar: ItemScene is NULL. Assign WorldItem scene in the HotBar inspector.");
            return false;
        }

        WorldItem worldItem = worldItemScene.Instantiate<WorldItem>();

        if (worldItem == null)
        {
            GD.PushError("HotBar: Failed to instantiate WorldItem. Make sure ItemScene root has WorldItem script.");
            return false;
        }

        GetTree().CurrentScene.AddChild(worldItem);

        worldItem.ItemInstance = item;

        worldItem.GlobalPosition =
            cam.GlobalPosition + forward * 1.2f + Vector3.Up * 0.2f;

        worldItem.Sleeping = false;
        worldItem.CanSleep = false;

        Vector3 throwForce = forward * 3.0f + Vector3.Up * 1.5f;

        worldItem.ApplyImpulse(throwForce, Vector3.Zero);

        worldItem.AngularVelocity = new Vector3(
            (float)GD.RandRange(-2.0, 2.0),
            (float)GD.RandRange(-2.0, 2.0),
            (float)GD.RandRange(-2.0, 2.0)
        );

        GD.Print("HotBar: WorldItem spawned and item instance assigned");

        return true;
    }

    // Debug prints the selected slot contents.
    private void PrintSelectedSlotInfo()
    {
        ItemInstance item = _hotbarItems[_selectedIndex];

        if (item == null)
        {
            GD.Print($"HotBar: Slot {_selectedIndex} EMPTY");
            return;
        }

        GD.Print(
            $"HotBar: Slot {_selectedIndex} | " +
            $"Item: {item.Definition.ItemName} | " +
            $"Icon: {(item.Definition.Icon != null ? item.Definition.Icon.ResourcePath : "null")} | " +
            $"WorldModel: {(item.Definition.WorldModel != null ? item.Definition.WorldModel.ResourcePath : "null")}"
        );
    }

    // Debug prints all hotbar slots.
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

    // Shows HUD action prompts for the currently selected item.
    private void UpdateHotbarActionPrompt()
    {
        HideHotbarActionPrompts();

        if (_playerHUD == null)
            return;

        ItemInstance item = _hotbarItems[_selectedIndex];

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

    // Hides all hotbar-related HUD prompts.
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

    // Executes the first visible/usable action on the selected item.
    public void UseSelectedItem()
    {
        if (IsInventoryMode() || IsItemUIMode())
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

            if (!action.CanExecute(player, item))
                continue;

            ExecuteAction(item, action);
            return;
        }
    }

    // Updates the RichTextLabel with the selected item name, or "none" when the slot is empty.
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

    // Public wrapper in case another script already calls this old method name.
    public void GetSelectedItemName()
    {
        UpdateSelectedItemNameText();
    }

    // Returns the currently selected slot index.
    public int GetSelectedIndex()
    {
        return _selectedIndex;
    }

    // True when the inventory UI is open.
    private bool IsInventoryMode()
    {
        return robinsonGlobals.Instance != null && robinsonGlobals.Instance.OpenInventory ;
    }

    // True when an item-specific UI is open.
    private bool IsItemUIMode()
    {
        return robinsonGlobals.Instance != null && robinsonGlobals.Instance.OpenItemUI ;
    }
}
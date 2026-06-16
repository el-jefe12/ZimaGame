using Godot;
using System.Collections.Generic;

public partial class ContainerInventoryPanel : PanelContainer
{
	// Safer than exporting InventoryItemSlot directly.
	// Assign the NodePath to the actual InventoryItemSlot template node.
	[Export]
	public NodePath SlotTemplatePath;

	[Export]
	public NodePath InventoryGridPath;

	[Export]
	public string PlayerGroupName = "player";

	[Export]
	public string HotbarGroupName = "hotbar";

	[Export]
	public string ContainerPanelGroupName = "container_inventory_panel";

	private InventoryItemSlot _slotTemplate;
	private Control _inventoryGrid;

	private ContainerInventory _containerInventory;
	private PlayerInventory _playerInventory;

	private readonly List<ItemInstance> _visibleItems = new List<ItemInstance>();

	private bool _manualDragActive = false;
	private ItemInstance _manualDragItem = null;
	private CanvasLayer _manualDragLayer = null;
	private TextureRect _manualDragPreview = null;

	public override void _Ready()
	{
		GD.Print("ContainerInventoryPanel: Ready");

		AddToGroup(ContainerPanelGroupName);

		FindInventoryGrid();
		FindSlotTemplate();
		FindPlayerInventory();

		if (_inventoryGrid == null)
		{
			GD.PushError(
				"ContainerInventoryPanel: Could not find InventoryGrid. " +
				"Assign InventoryGridPath, or create a child Control named InventoryGrid."
			);
			return;
		}

		if (_slotTemplate == null)
		{
			GD.PushError(
				"ContainerInventoryPanel: Slot template was not found. " +
				"SlotTemplatePath must point to the actual InventoryItemSlot node."
			);
			return;
		}

		if (_playerInventory == null)
		{
			GD.PushError("ContainerInventoryPanel: PlayerInventory was not found.");
			return;
		}

		_slotTemplate.Visible = false;

		// The panel starts empty until a real container is opened.
		Visible = false;
	}

	public override void _ExitTree()
	{
		CleanupManualInventoryDrag();
		DisconnectContainerSignals();
	}

	public override void _Process(double delta)
	{
		if (_manualDragActive)
			UpdateManualDragPreviewPosition();
	}

	public override void _Input(InputEvent inputEvent)
	{
		if (!_manualDragActive)
			return;

		if (inputEvent is not InputEventMouseButton mouseButton)
			return;

		if (mouseButton.ButtonIndex != MouseButton.Left)
			return;

		if (mouseButton.Pressed)
			return;

		FinishManualContainerDrag();
		GetViewport().SetInputAsHandled();
	}

	private void FindInventoryGrid()
	{
		_inventoryGrid = null;

		if (InventoryGridPath != null && !InventoryGridPath.IsEmpty)
			_inventoryGrid = GetNodeOrNull<Control>(InventoryGridPath);

		if (_inventoryGrid != null)
			return;

		_inventoryGrid = GetNodeOrNull<Control>("InventoryGrid");

		if (_inventoryGrid != null)
			return;

		_inventoryGrid = GetNodeOrNull<Control>("%InventoryGrid");

		if (_inventoryGrid != null)
			return;

		_inventoryGrid = FindChild("InventoryGrid", true, false) as Control;
	}

	private void FindSlotTemplate()
	{
		_slotTemplate = null;

		if (SlotTemplatePath != null && !SlotTemplatePath.IsEmpty)
			_slotTemplate = GetNodeOrNull<InventoryItemSlot>(SlotTemplatePath);

		if (_slotTemplate != null)
			return;

		_slotTemplate = GetNodeOrNull<InventoryItemSlot>("InventoryItemSlot");

		if (_slotTemplate != null)
			return;

		_slotTemplate = GetNodeOrNull<InventoryItemSlot>("SlotTemplate");

		if (_slotTemplate != null)
			return;

		_slotTemplate = GetNodeOrNull<InventoryItemSlot>("%InventoryItemSlot");

		if (_slotTemplate != null)
			return;

		_slotTemplate = GetNodeOrNull<InventoryItemSlot>("%SlotTemplate");

		if (_slotTemplate != null)
			return;

		_slotTemplate = FindChild("InventoryItemSlot", true, false) as InventoryItemSlot;

		if (_slotTemplate != null)
			return;

		_slotTemplate = FindChild("SlotTemplate", true, false) as InventoryItemSlot;
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

	public void OpenContainer(ContainerInventory containerInventory)
	{
		DisconnectContainerSignals();

		_containerInventory = containerInventory;

		if (_containerInventory == null)
		{
			GD.PushError("ContainerInventoryPanel: Tried to open a null container.");
			return;
		}

		_containerInventory.ItemAdded += OnContainerItemAdded;
		_containerInventory.ItemRemoved += OnContainerItemRemoved;
		_containerInventory.ContainerChanged += OnContainerChanged;

		Visible = true;

		RebuildContainerFromList();

		GD.Print("ContainerInventoryPanel: Opened container.");
	}

	public void CloseContainer()
	{
		CleanupManualInventoryDrag();
		DisconnectContainerSignals();

		_containerInventory = null;

		ClearInventoryGrid();

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

		if (_inventoryGrid == null)
			return;

		if (_slotTemplate == null)
			return;

		ClearInventoryGrid();

		_visibleItems.Clear();

		for (int i = 0; i < _containerInventory.Items.Count; i++)
		{
			ItemInstance item = _containerInventory.Items[i];

			if (item == null || item.Definition == null)
			{
				GD.Print($"ContainerInventoryPanel: Skipping null item at index {i}");
				continue;
			}

			_visibleItems.Add(item);
			AddSlotForItem(item, i);
		}

		GD.Print($"ContainerInventoryPanel: Rebuilt. Visible items: {_visibleItems.Count}");
	}

	private void AddSlotForItem(ItemInstance item, int index)
	{
		Node duplicatedNode = _slotTemplate.Duplicate();

		InventoryItemSlot slot = duplicatedNode as InventoryItemSlot;

		if (slot == null)
		{
			GD.PushError(
				"ContainerInventoryPanel: Slot template duplicate is not InventoryItemSlot. " +
				"Make sure the template node itself has InventoryItemSlot.cs attached."
			);

			duplicatedNode.QueueFree();
			return;
		}

		slot.Name = $"ContainerItemSlot_{index}";
		slot.Visible = true;

		_inventoryGrid.AddChild(slot);

		slot.SetItem(item);

		// In the container panel, remove/action both mean "take item".
		// You can split this later if you want right-click to inspect/use instead.
		slot.RemoveRequested += OnSlotTakeRequested;
		slot.ActionExecuted += OnSlotTakeRequested;
		slot.DragRequested += OnSlotDragRequested;

		GD.Print(
			$"ContainerInventoryPanel: Added slot {index} | " +
			$"Item: {item.Definition.ItemName} | " +
			$"Icon: {(item.Definition.Icon != null ? item.Definition.Icon.ResourcePath : "NULL")}"
		);
	}

	private void ClearInventoryGrid()
	{
		if (_inventoryGrid == null)
			return;

		foreach (Node child in _inventoryGrid.GetChildren())
		{
			if (child == _slotTemplate)
				continue;

			if (child is InventoryItemSlot slot)
			{
				slot.RemoveRequested -= OnSlotTakeRequested;
				slot.ActionExecuted -= OnSlotTakeRequested;
				slot.DragRequested -= OnSlotDragRequested;
			}

			child.QueueFree();
		}
	}

	private void OnSlotTakeRequested(ItemInstance item)
	{
		TakeItemFromContainer(item);
	}

	private void TakeItemFromContainer(ItemInstance item)
	{
		if (_containerInventory == null)
			return;

		if (_playerInventory == null)
			return;

		if (item == null || item.Definition == null)
			return;

		bool removedFromContainer = _containerInventory.RemoveItemInstance(item);

		if (!removedFromContainer)
		{
			GD.Print("ContainerInventoryPanel: Could not remove item from container.");
			return;
		}

		_playerInventory.AddItemInstance(item);

		GD.Print($"ContainerInventoryPanel: Took item from container: {item.Definition.ItemName}");
	}

	private void OnSlotDragRequested(ItemInstance item)
	{
		if (item == null || item.Definition == null)
			return;

		StartManualContainerDrag(item);
	}

	private void StartManualContainerDrag(ItemInstance item)
	{
		if (_manualDragActive)
			return;

		GD.Print($"ContainerInventoryPanel: Drag started: {item.Definition.ItemName}");

		_manualDragActive = true;
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

	private void FinishManualContainerDrag()
	{
		if (!_manualDragActive)
			return;

		ItemInstance draggedItem = _manualDragItem;

		if (draggedItem == null || draggedItem.Definition == null)
		{
			CleanupManualInventoryDrag();
			return;
		}

		// Dragging from container to hotbar.
		HotBarScript hotbar = GetTree().GetFirstNodeInGroup(HotbarGroupName) as HotBarScript;

		if (hotbar != null)
		{
			bool movedToHotbar = hotbar.TryAcceptInventoryItemAtMouse(draggedItem);

			if (movedToHotbar)
			{
				GD.Print($"ContainerInventoryPanel: Moved container item to hotbar: {draggedItem.Definition.ItemName}");

				if (_containerInventory != null)
					_containerInventory.RemoveItemInstance(draggedItem);

				CleanupManualInventoryDrag();
				return;
			}
		}

		// Dragging from container to player inventory.
		InventoryContainerScript playerInventoryPanel =
			GetTree().GetFirstNodeInGroup("inventory_container") as InventoryContainerScript;

		if (playerInventoryPanel != null && playerInventoryPanel.IsMouseOverInventory())
		{
			TakeItemFromContainer(draggedItem);
			CleanupManualInventoryDrag();
			return;
		}

		GD.Print("ContainerInventoryPanel: Drag released outside valid target. Item stays in container.");

		CleanupManualInventoryDrag();
	}

	private void CleanupManualInventoryDrag()
	{
		_manualDragActive = false;
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

	public bool TryAcceptPlayerInventoryItem(ItemInstance item)
	{
		if (_containerInventory == null)
			return false;

		if (_playerInventory == null)
			return false;

		if (item == null || item.Definition == null)
			return false;

		if (!IsMouseOverContainer())
			return false;

		bool removedFromPlayer = _playerInventory.RemoveItemInstance(item);

		if (!removedFromPlayer)
			return false;

		_containerInventory.AddItemInstance(item);

		GD.Print($"ContainerInventoryPanel: Moved player item to container: {item.Definition.ItemName}");

		return true;
	}

	public bool TryAcceptHotbarItem(ItemInstance item)
	{
		if (_containerInventory == null)
			return false;

		if (item == null || item.Definition == null)
			return false;

		if (!IsMouseOverContainer())
			return false;

		_containerInventory.AddItemInstance(item);

		GD.Print($"ContainerInventoryPanel: Accepted hotbar item into container: {item.Definition.ItemName}");

		return true;
	}

	public bool IsMouseOverContainer()
	{
		Vector2 mousePosition = GetViewport().GetMousePosition();

		if (GetGlobalRect().HasPoint(mousePosition))
			return true;

		if (_inventoryGrid != null && _inventoryGrid.GetGlobalRect().HasPoint(mousePosition))
			return true;

		return false;
	}

	public IReadOnlyList<ItemInstance> GetVisibleItems()
	{
		return _visibleItems;
	}
}
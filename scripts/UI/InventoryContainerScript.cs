using Godot;
using System.Collections.Generic;

public partial class InventoryContainerScript : PanelContainer
{
	// Drag your already existing InventoryItemSlot node here in the inspector.
	// This is NOT a PackedScene. This is a template node already in the scene tree.
	[Export]
	public InventoryItemSlot SlotTemplate;

	// Assign your WorldItem scene here.
	[Export]
	public PackedScene WorldItemScene;

	[Export]
	public NodePath InventoryGridPath;

	[Export]
	public string PlayerGroupName = "player";

	[Export]
	public string HotbarGroupName = "hotbar";

	[Export]
	public string InventoryContainerGroupName = "inventory_container";

	private PlayerInventory _inventory;
	private Control _inventoryGrid;

	private readonly List<ItemInstance> _visibleItems = new List<ItemInstance>();

	private bool _manualDragActive = false;
	private ItemInstance _manualDragItem = null;
	private CanvasLayer _manualDragLayer = null;
	private TextureRect _manualDragPreview = null;

	public override void _Ready()
	{
		GD.Print("InventoryContainer: Ready");

		AddToGroup(InventoryContainerGroupName);

		FindInventoryGrid();

		if (_inventoryGrid == null)
		{
			GD.PushError(
				"InventoryContainer: Could not find inventory grid. " +
				"Assign InventoryGridPath, or create a child Control named InventoryGrid."
			);
			return;
		}

		if (SlotTemplate == null)
		{
			GD.PushError(
				"InventoryContainer: SlotTemplate is NULL. " +
				"Drag your existing InventoryItemSlot node into SlotTemplate in the inspector."
			);
			return;
		}

		if (WorldItemScene == null)
		{
			GD.PushWarning(
				"InventoryContainer: WorldItemScene is NULL. " +
				"Removing items will not drop them until you assign the WorldItem scene."
			);
		}

		SlotTemplate.Visible = false;

		FindPlayerInventory();

		if (_inventory == null)
		{
			GD.PushError("InventoryContainer: PlayerInventory was not found.");
			return;
		}

		_inventory.ItemAdded += OnInventoryItemAdded;
		_inventory.ItemRemoved += OnInventoryItemRemoved;
		_inventory.InventoryChanged += OnInventoryChanged;

		RebuildInventoryFromList();
	}

	public override void _ExitTree()
	{
		CleanupManualInventoryDrag();

		if (_inventory == null)
			return;

		_inventory.ItemAdded -= OnInventoryItemAdded;
		_inventory.ItemRemoved -= OnInventoryItemRemoved;
		_inventory.InventoryChanged -= OnInventoryChanged;
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

		FinishManualInventoryDrag();
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

	private void FindPlayerInventory()
	{
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

		if (_inventoryGrid == null)
			return;

		if (SlotTemplate == null)
			return;

		ClearInventoryGrid();

		_visibleItems.Clear();

		for (int i = 0; i < _inventory.playerInventory.Count; i++)
		{
			ItemInstance item = _inventory.playerInventory[i];

			if (item == null || item.Definition == null)
			{
				GD.Print($"InventoryContainer: Skipping null item at index {i}");
				continue;
			}

			_visibleItems.Add(item);
			AddSlotForItem(item, i);
		}

		GD.Print($"InventoryContainer: Rebuilt. Visible items: {_visibleItems.Count}");
	}

	private void AddSlotForItem(ItemInstance item, int index)
	{
		Node duplicatedNode = SlotTemplate.Duplicate();

		InventoryItemSlot slot = duplicatedNode as InventoryItemSlot;

		if (slot == null)
		{
			GD.PushError(
				"InventoryContainer: SlotTemplate duplicate is not InventoryItemSlot. " +
				"Make sure your template node has InventoryItemSlot.cs attached to its root."
			);

			duplicatedNode.QueueFree();
			return;
		}

		slot.Name = $"InventoryItemSlot_{index}";
		slot.Visible = true;

		_inventoryGrid.AddChild(slot);

		slot.SetItem(item);

		slot.RemoveRequested += OnSlotRemoveRequested;
		slot.ActionExecuted += OnSlotActionExecuted;
		slot.DragRequested += OnSlotDragRequested;

		GD.Print(
			$"InventoryContainer: Added slot {index} | " +
			$"Item: {item.Definition.ItemName} | " +
			$"Icon: {(item.Definition.Icon != null ? item.Definition.Icon.ResourcePath : "NULL")}"
		);
	}

	private void ClearInventoryGrid()
	{
		foreach (Node child in _inventoryGrid.GetChildren())
		{
			if (child == SlotTemplate)
				continue;

			if (child is InventoryItemSlot slot)
			{
				slot.RemoveRequested -= OnSlotRemoveRequested;
				slot.ActionExecuted -= OnSlotActionExecuted;
				slot.DragRequested -= OnSlotDragRequested;
			}

			child.QueueFree();
		}
	}

	private void OnSlotDragRequested(ItemInstance item)
	{
		if (item == null || item.Definition == null)
			return;

		StartManualInventoryDrag(item);
	}

	private void StartManualInventoryDrag(ItemInstance item)
	{
		if (_manualDragActive)
			return;

		GD.Print($"InventoryContainer: Drag started: {item.Definition.ItemName}");

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

	private void FinishManualInventoryDrag()
	{
		if (!_manualDragActive)
			return;

		ItemInstance draggedItem = _manualDragItem;

		if (draggedItem == null || draggedItem.Definition == null)
		{
			CleanupManualInventoryDrag();
			return;
		}

		HotBarScript hotbar = GetTree().GetFirstNodeInGroup(HotbarGroupName) as HotBarScript;

		if (hotbar != null)
		{
			bool movedToHotbar = hotbar.TryAcceptInventoryItemAtMouse(draggedItem);

			if (movedToHotbar)
			{
				GD.Print($"InventoryContainer: Moved item to hotbar: {draggedItem.Definition.ItemName}");

				_inventory.RemoveItemInstance(draggedItem);

				CleanupManualInventoryDrag();
				return;
			}
		}

		GD.Print("InventoryContainer: Drag released outside valid hotbar slot. Item stays in inventory.");

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
		Vector2 mousePosition = GetViewport().GetMousePosition();

		if (GetGlobalRect().HasPoint(mousePosition))
			return true;

		if (_inventoryGrid != null && _inventoryGrid.GetGlobalRect().HasPoint(mousePosition))
			return true;

		return false;
	}

	private void OnSlotRemoveRequested(ItemInstance item)
	{
		if (_inventory == null)
			return;

		if (item == null || item.Definition == null)
			return;

		GD.Print($"InventoryContainer: Drop requested: {item.Definition.ItemName}");

		bool dropped = SpawnWorldItem(item);

		if (!dropped)
		{
			GD.Print("InventoryContainer: Drop failed. Item stays in inventory.");
			return;
		}

		_inventory.RemoveItemInstance(item);
	}

	private void OnSlotActionExecuted(ItemInstance item)
	{
		if (_inventory == null)
			return;

		if (item == null || item.Definition == null)
			return;

		GD.Print($"InventoryContainer: Action executed: {item.Definition.ItemName}");

		// Consumable items should disappear from the backpack after being used.
		// This catches food/drink/medicine items that use ConsumableData, like EatAction.
		if (item.Definition.ConsumableData != null)
		{
			GD.Print($"InventoryContainer: Consumed item removed from inventory: {item.Definition.ItemName}");
			_inventory.RemoveItemInstance(item);
			return;
		}

		// Non-consumable actions can stay in inventory.
		RebuildInventoryFromList();
	}

	private bool SpawnWorldItem(ItemInstance item)
	{
		if (item == null || item.Definition == null)
			return false;

		if (WorldItemScene == null)
		{
			GD.PushError("InventoryContainer: WorldItemScene is NULL. Assign your WorldItem scene in the inspector.");
			return false;
		}

		Camera3D camera = GetViewport().GetCamera3D();

		if (camera == null)
		{
			GD.PushError("InventoryContainer: Could not find current Camera3D.");
			return false;
		}

		WorldItem worldItem = WorldItemScene.Instantiate<WorldItem>();

		if (worldItem == null)
		{
			GD.PushError(
				"InventoryContainer: Failed to instantiate WorldItem. " +
				"Make sure WorldItemScene root has WorldItem.cs attached."
			);
			return false;
		}

		Node currentScene = GetTree().CurrentScene;

		if (currentScene == null)
		{
			worldItem.QueueFree();
			GD.PushError("InventoryContainer: CurrentScene is NULL.");
			return false;
		}

		currentScene.AddChild(worldItem);

		worldItem.ItemInstance = item;

		Vector3 forward = -camera.GlobalTransform.Basis.Z;

		worldItem.GlobalPosition =
			camera.GlobalPosition + forward * 1.2f + Vector3.Up * 0.2f;

		worldItem.Sleeping = false;
		worldItem.CanSleep = false;

		Vector3 throwForce = forward * 3.0f + Vector3.Up * 1.5f;

		worldItem.ApplyImpulse(throwForce, Vector3.Zero);

		worldItem.AngularVelocity = new Vector3(
			(float)GD.RandRange(-2.0, 2.0),
			(float)GD.RandRange(-2.0, 2.0),
			(float)GD.RandRange(-2.0, 2.0)
		);

		GD.Print($"InventoryContainer: Dropped item into world: {item.Definition.ItemName}");

		return true;
	}

	public IReadOnlyList<ItemInstance> GetVisibleItems()
	{
		return _visibleItems;
	}
}

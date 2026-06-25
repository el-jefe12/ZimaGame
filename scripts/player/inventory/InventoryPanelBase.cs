using Godot;
using System.Collections.Generic;

public abstract partial class InventoryPanelBase : PanelContainer
{
    [ExportCategory("Shared Inventory Panel")]
    [Export] public NodePath SlotTemplatePath;
    [Export] public NodePath InventoryGridPath;

    protected InventoryItemSlot SlotTemplate;
    protected Control InventoryGrid;

    protected readonly List<ItemInstance> VisibleItems = new List<ItemInstance>();

    protected bool ManualDragActive = false;
    protected ItemInstance ManualDragItem = null;
    protected CanvasLayer ManualDragLayer = null;
    protected TextureRect ManualDragPreview = null;

    public override void _Process(double delta)
    {
        if (ManualDragActive)
            UpdateManualDragPreviewPosition();
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (!ManualDragActive)
            return;

        if (inputEvent is not InputEventMouseButton mouseButton)
            return;

        if (mouseButton.ButtonIndex != MouseButton.Left)
            return;

        if (mouseButton.Pressed)
            return;

        FinishManualDrag();
        GetViewport().SetInputAsHandled();
    }

    protected void SetupSharedPanel(InventoryItemSlot directSlotTemplate = null)
    {
        FindInventoryGrid();
        FindSlotTemplate(directSlotTemplate);

        if (InventoryGrid == null)
        {
            GD.PushError($"{Name}: Could not find InventoryGrid.");
            return;
        }

        if (SlotTemplate == null)
        {
            GD.PushError($"{Name}: Could not find slot template.");
            return;
        }

        SlotTemplate.Visible = false;
    }

    private void FindInventoryGrid()
    {
        InventoryGrid = null;

        if (InventoryGridPath != null && !InventoryGridPath.IsEmpty)
            InventoryGrid = GetNodeOrNull<Control>(InventoryGridPath);

        if (InventoryGrid != null)
            return;

        InventoryGrid = GetNodeOrNull<Control>("InventoryGrid");

        if (InventoryGrid != null)
            return;

        InventoryGrid = GetNodeOrNull<Control>("%InventoryGrid");

        if (InventoryGrid != null)
            return;

        InventoryGrid = FindChild("InventoryGrid", true, false) as Control;
    }

    private void FindSlotTemplate(InventoryItemSlot directSlotTemplate)
    {
        SlotTemplate = null;

        if (directSlotTemplate != null)
        {
            SlotTemplate = directSlotTemplate;
            return;
        }

        if (SlotTemplatePath != null && !SlotTemplatePath.IsEmpty)
            SlotTemplate = GetNodeOrNull<InventoryItemSlot>(SlotTemplatePath);

        if (SlotTemplate != null)
            return;

        SlotTemplate = GetNodeOrNull<InventoryItemSlot>("InventoryItemSlot");

        if (SlotTemplate != null)
            return;

        SlotTemplate = GetNodeOrNull<InventoryItemSlot>("SlotTemplate");

        if (SlotTemplate != null)
            return;

        SlotTemplate = GetNodeOrNull<InventoryItemSlot>("%InventoryItemSlot");

        if (SlotTemplate != null)
            return;

        SlotTemplate = GetNodeOrNull<InventoryItemSlot>("%SlotTemplate");

        if (SlotTemplate != null)
            return;

        SlotTemplate = FindChild("InventoryItemSlot", true, false) as InventoryItemSlot;

        if (SlotTemplate != null)
            return;

        SlotTemplate = FindChild("SlotTemplate", true, false) as InventoryItemSlot;
    }

    protected void RebuildFromItems(IReadOnlyList<ItemInstance> items, string slotNamePrefix)
    {
        if (items == null)
            return;

        if (InventoryGrid == null)
            return;

        if (SlotTemplate == null)
            return;

        ClearGrid();

        VisibleItems.Clear();

        for (int i = 0; i < items.Count; i++)
        {
            ItemInstance item = items[i];

            if (item == null || item.Definition == null)
            {
                GD.Print($"{Name}: Skipping null item at index {i}");
                continue;
            }

            VisibleItems.Add(item);
            AddSlotForItem(item, i, slotNamePrefix);
        }

        GD.Print($"{Name}: Rebuilt. Visible items: {VisibleItems.Count}");
    }

    protected virtual void AddSlotForItem(ItemInstance item, int index, string slotNamePrefix)
    {
        Node duplicatedNode = SlotTemplate.Duplicate();

        InventoryItemSlot slot = duplicatedNode as InventoryItemSlot;

        if (slot == null)
        {
            GD.PushError($"{Name}: Slot template duplicate is not InventoryItemSlot.");
            duplicatedNode.QueueFree();
            return;
        }

        slot.Name = $"{slotNamePrefix}_{index}";
        slot.Visible = true;

        InventoryGrid.AddChild(slot);

        slot.SetItem(item);

        slot.RemoveRequested += OnSlotRemoveRequested;
        slot.ActionExecuted += OnSlotActionRequested;
        slot.DragRequested += OnSlotDragRequested;

        GD.Print(
            $"{Name}: Added slot {index} | " +
            $"Item: {item.Definition.ItemName} | " +
            $"Icon: {(item.Definition.Icon != null ? item.Definition.Icon.ResourcePath : "NULL")}"
        );
    }

    protected void ClearGrid()
    {
        if (InventoryGrid == null)
            return;

        foreach (Node child in InventoryGrid.GetChildren())
        {
            if (child == SlotTemplate)
                continue;

            if (child is InventoryItemSlot slot)
            {
                slot.RemoveRequested -= OnSlotRemoveRequested;
                slot.ActionExecuted -= OnSlotActionRequested;
                slot.DragRequested -= OnSlotDragRequested;
            }

            child.QueueFree();
        }
    }

    protected void StartManualDrag(ItemInstance item)
    {
        if (ManualDragActive)
            return;

        if (item == null || item.Definition == null)
            return;

        GD.Print($"{Name}: Drag started: {item.Definition.ItemName}");

        ManualDragActive = true;
        ManualDragItem = item;

        if (ManualDragLayer != null)
            ManualDragLayer.QueueFree();

        ManualDragLayer = new CanvasLayer();
        ManualDragLayer.Layer = 1000;
        GetTree().Root.AddChild(ManualDragLayer);

        ManualDragPreview = new TextureRect();
        ManualDragPreview.Texture = item.Definition.Icon;
        ManualDragPreview.Size = new Vector2(56.0f, 56.0f);
        ManualDragPreview.CustomMinimumSize = new Vector2(56.0f, 56.0f);
        ManualDragPreview.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
        ManualDragPreview.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
        ManualDragPreview.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.75f);
        ManualDragPreview.MouseFilter = MouseFilterEnum.Ignore;

        ManualDragLayer.AddChild(ManualDragPreview);

        UpdateManualDragPreviewPosition();
    }

    protected void CleanupManualDrag()
    {
        ManualDragActive = false;
        ManualDragItem = null;

        if (ManualDragLayer != null)
        {
            ManualDragLayer.QueueFree();
            ManualDragLayer = null;
        }

        ManualDragPreview = null;
    }

    private void UpdateManualDragPreviewPosition()
    {
        if (ManualDragPreview == null)
            return;

        Vector2 mousePosition = GetViewport().GetMousePosition();

        ManualDragPreview.GlobalPosition =
            mousePosition - (ManualDragPreview.Size * 0.5f);
    }

    public bool IsMouseOverPanel()
    {
        Vector2 mousePosition = GetViewport().GetMousePosition();

        if (GetGlobalRect().HasPoint(mousePosition))
            return true;

        if (InventoryGrid != null && InventoryGrid.GetGlobalRect().HasPoint(mousePosition))
            return true;

        return false;
    }

    public IReadOnlyList<ItemInstance> GetVisibleItems()
    {
        return VisibleItems;
    }

    protected abstract void OnSlotRemoveRequested(ItemInstance item);
    protected abstract void OnSlotActionRequested(ItemInstance item);
    protected abstract void OnSlotDragRequested(ItemInstance item);
    protected abstract void FinishManualDrag();
}
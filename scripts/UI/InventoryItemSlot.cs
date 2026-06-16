using Godot;
using System;

public partial class InventoryItemSlot : PanelContainer
{
    [Signal]
    public delegate void RemoveRequestedEventHandler(ItemInstance item);

    [Signal]
    public delegate void ActionExecutedEventHandler(ItemInstance item);

	[Signal]
	public delegate void DragRequestedEventHandler(ItemInstance item);

    [Export] public bool ShowRemoveOption = true;
    [Export] public string PlayerGroupName = "player";

    private ItemInstance _item;

    private TextureRect _itemIcon;
    private RichTextLabel _itemName;
    private RichTextLabel _itemHealth;
    private RichTextLabel _itemAmount;
    private RichTextLabel _itemWeight;
    private TextureRect _itemClass;

    private PopupMenu _popupMenu;
    private bool _popupConnected = false;

    private const int RemoveOptionId = 99999;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;

        CacheExistingNodes();
        MakeChildrenIgnoreMouse(this);
        CreatePopupMenu();

        RefreshVisuals();

        GD.Print("InventoryItemSlot: Ready");
    }

    public override void _ExitTree()
    {
        if (_popupMenu != null && _popupConnected)
        {
            _popupMenu.IdPressed -= OnPopupMenuIdPressed;
            _popupConnected = false;
        }
    }

	public override void _GuiInput(InputEvent inputEvent)
	{
		if (inputEvent is not InputEventMouseButton mouseButton)
			return;

		if (!mouseButton.Pressed)
			return;

		if (_item == null || _item.Definition == null)
			return;

		if (mouseButton.ButtonIndex == MouseButton.Left)
		{
			EmitSignal(SignalName.DragRequested, _item);
			GetViewport().SetInputAsHandled();
			return;
		}

		if (mouseButton.ButtonIndex == MouseButton.Right)
		{
			ShowActionMenu();
			GetViewport().SetInputAsHandled();
			return;
		}
	}

    public void SetItem(ItemInstance item)
    {
        _item = item;

        if (_item == null)
        {
            GD.Print("InventoryItemSlot: SetItem NULL");
        }
        else if (_item.Definition == null)
        {
            GD.Print("InventoryItemSlot: SetItem has NULL definition");
        }
        else
        {
            GD.Print(
                $"InventoryItemSlot: SetItem {_item.Definition.ItemName} | " +
                $"Icon: {(_item.Definition.Icon != null ? _item.Definition.Icon.ResourcePath : "NULL")}"
            );
        }

        RefreshVisuals();
    }

    public ItemInstance GetItem()
    {
        return _item;
    }

    private void CacheExistingNodes()
    {
        // Uses your existing scene nodes. Does not create anything.

        _itemIcon = GetNodeOrNull<TextureRect>("%ItemIcon");
        _itemName = GetNodeOrNull<RichTextLabel>("%ItemName");

        _itemHealth = GetNodeOrNull<RichTextLabel>("%ItemHealth");
        _itemAmount = GetNodeOrNull<RichTextLabel>("%ItemAmount");
        _itemWeight = GetNodeOrNull<RichTextLabel>("%ItemWeight");

        _itemClass = GetNodeOrNull<TextureRect>("%ItemClass");

        // Fallbacks, in case paths change slightly later.
        _itemIcon ??= FindChild("ItemIcon", true, false) as TextureRect;
        _itemName ??= FindChild("ItemName", true, false) as RichTextLabel;
        _itemHealth ??= FindChild("ItemHealth", true, false) as RichTextLabel;
        _itemAmount ??= FindChild("ItemAmount", true, false) as RichTextLabel;
        _itemWeight ??= FindChild("ItemWeight", true, false) as RichTextLabel;
        _itemClass ??= FindChild("ItemClass", true, false) as TextureRect;

        if (_itemIcon == null)
            GD.PushWarning("InventoryItemSlot: Existing node ItemIcon was not found.");

        if (_itemName == null)
            GD.PushWarning("InventoryItemSlot: Existing node ItemName was not found.");

        if (_itemHealth == null)
            GD.PushWarning("InventoryItemSlot: Existing node ItemHealth was not found.");

        if (_itemAmount == null)
            GD.PushWarning("InventoryItemSlot: Existing node ItemAmount was not found.");

        if (_itemWeight == null)
            GD.PushWarning("InventoryItemSlot: Existing node ItemWeight was not found.");

        if (_itemClass == null)
            GD.PushWarning("InventoryItemSlot: Existing node ItemClass was not found.");
    }

    private void RefreshVisuals()
    {
        CacheExistingNodes();

        if (_item == null || _item.Definition == null)
        {
            ClearVisuals();
            return;
        }

        Item definition = _item.Definition;

        if (_itemIcon != null)
        {
            _itemIcon.Texture = definition.Icon;
            _itemIcon.Visible = definition.Icon != null;
        }

        if (_itemName != null)
        {
            _itemName.Text = definition.ItemName ?? "";
            _itemName.Visible = !string.IsNullOrWhiteSpace(_itemName.Text);
        }

        if (_itemHealth != null)
        {
            // Keep this simple for now.
            // If ItemHealth is not what you want displayed, change only this text.
            string healthText = definition.ItemHealth != null
                ? $"{definition.ItemHealth}"
                : "";

            _itemHealth.Text = healthText;
            _itemHealth.Visible = !string.IsNullOrWhiteSpace(healthText);
        }

        if (_itemAmount != null)
        {

            string amountText = definition.ItemAmount != null
                ? $"{definition.ItemAmount}"
                : "";

            _itemAmount.Text = amountText;
            _itemAmount.Visible = !string.IsNullOrWhiteSpace(amountText);
        }

        if (_itemWeight != null)
        {
            string weightText = definition.ItemWeight != null
                ? $"{definition.ItemWeight}"
                : "";

            _itemWeight.Text = weightText + " Kg";
            _itemWeight.Visible = !string.IsNullOrWhiteSpace(weightText);
        }

        if (_itemClass != null)
        {
            string classText = definition.Item_SubType != null
                ? $"{definition.Item_SubType}"
                : "";

            // Only show it if you already assigned a class texture in the scene.
            _itemClass.Visible = _itemClass.Texture != null;
        }
    }

    private void ClearVisuals()
    {
        if (_itemIcon != null)
        {
            _itemIcon.Texture = null;
            _itemIcon.Visible = false;
        }

        if (_itemName != null)
        {
            _itemName.Text = "";
            _itemName.Visible = false;
        }

        if (_itemHealth != null)
        {
            _itemHealth.Text = "";
            _itemHealth.Visible = false;
        }

        if (_itemAmount != null)
        {
            _itemAmount.Text = "";
            _itemAmount.Visible = false;
        }

        if (_itemWeight != null)
        {
            _itemWeight.Text = "";
            _itemWeight.Visible = false;
        }

        if (_itemClass != null)
            _itemClass.Visible = false;
    }

    private void CreatePopupMenu()
    {
        _popupMenu = GetNodeOrNull<PopupMenu>("ItemActionPopup");

        if (_popupMenu == null)
        {
            _popupMenu = new PopupMenu();
            _popupMenu.Name = "ItemActionPopup";
            AddChild(_popupMenu);
        }

        if (!_popupConnected)
        {
            _popupMenu.IdPressed += OnPopupMenuIdPressed;
            _popupConnected = true;
        }
    }

    private void ShowActionMenu()
    {
        if (_item == null || _item.Definition == null)
            return;

        if (_popupMenu == null)
            CreatePopupMenu();

        _popupMenu.Clear();

        int menuId = 0;

        if (_item.Definition.Actions != null)
        {
            Node player = GetPlayerNode();

            for (int i = 0; i < _item.Definition.Actions.Count; i++)
            {
                ItemAction action = _item.Definition.Actions[i];

                if (action == null)
                    continue;

                if (player != null && !action.CanExecute(player, _item))
                    continue;

                _popupMenu.AddItem(action.ActionName, menuId);
                menuId++;
            }
        }

        if (ShowRemoveOption)
        {
            if (_popupMenu.ItemCount > 0)
                _popupMenu.AddSeparator();

            _popupMenu.AddItem("Remove", RemoveOptionId);
        }

        if (_popupMenu.ItemCount <= 0)
            return;

        Vector2 mousePosition = GetViewport().GetMousePosition();

        _popupMenu.Position = new Vector2I(
            Mathf.RoundToInt(mousePosition.X),
            Mathf.RoundToInt(mousePosition.Y)
        );

        _popupMenu.Popup();
    }

    private void OnPopupMenuIdPressed(long id)
    {
        if (_item == null || _item.Definition == null)
            return;

        if (id == RemoveOptionId)
        {
            EmitSignal(SignalName.RemoveRequested, _item);
            return;
        }

        ItemAction action = GetExecutableActionByMenuId((int)id);

        if (action == null)
            return;

        Node player = GetPlayerNode();

        if (player == null)
        {
            GD.PushWarning("InventoryItemSlot: No player found in group.");
            return;
        }

        if (!action.CanExecute(player, _item))
            return;

        action.Execute(player, _item);

        EmitSignal(SignalName.ActionExecuted, _item);
    }

    private ItemAction GetExecutableActionByMenuId(int wantedMenuId)
    {
        if (_item == null || _item.Definition == null)
            return null;

        if (_item.Definition.Actions == null)
            return null;

        Node player = GetPlayerNode();

        int currentMenuId = 0;

        for (int i = 0; i < _item.Definition.Actions.Count; i++)
        {
            ItemAction action = _item.Definition.Actions[i];

            if (action == null)
                continue;

            if (player != null && !action.CanExecute(player, _item))
                continue;

            if (currentMenuId == wantedMenuId)
                return action;

            currentMenuId++;
        }

        return null;
    }

    private Node GetPlayerNode()
    {
        if (string.IsNullOrWhiteSpace(PlayerGroupName))
            return null;

        return GetTree().GetFirstNodeInGroup(PlayerGroupName);
    }

    private void MakeChildrenIgnoreMouse(Control root)
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is Control control)
            {
                control.MouseFilter = MouseFilterEnum.Ignore;
                MakeChildrenIgnoreMouse(control);
            }
        }
    }
}
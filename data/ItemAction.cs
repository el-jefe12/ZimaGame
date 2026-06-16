using Godot;
using System;

[Flags]
public enum ItemActionResult
{
    None = 0,
    RemoveItem = 1,
    RefreshHeldItem = 2,
    RefreshPrompt = 4,
}

[GlobalClass]
public abstract partial class ItemAction : Resource
{
    [Export] public string ActionName = "Use";

    // Which input triggers this action.
    [Export] public string InputActionName = "game_use_item_alt";

    // If true, this action appears as the main hotbar action prompt.
    [Export] public bool ShowInHotbarPrompt = true;

    [Export] public bool HoldRequired = false;
    [Export] public float HoldDuration = 1.2f;

    public virtual bool CanExecute(Node player, ItemInstance item)
    {
        return true;
    }

    public virtual string GetInteractionText(ItemInstance item)
    {
        if (item == null || item.Definition == null)
            return ActionName;

        return $"{ActionName} {item.Definition.ItemName}";
    }

    public abstract ItemActionResult Execute(Node player, ItemInstance item);
}
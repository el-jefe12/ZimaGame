using Godot;

[GlobalClass]
public partial class OpenContainerAction : ItemAction
{
    [ExportCategory("Groups")]
    [Export] public string ContainerPanelGroupName = "container_inventory_panel";

    public override ItemActionResult Execute(Node player, ItemInstance item)
    {
        if (player == null)
        {
            GD.PrintErr("OpenContainerAction: Player is null.");
            return ItemActionResult.None;
        }

        if (item == null)
        {
            GD.PrintErr("OpenContainerAction: Item is null.");
            return ItemActionResult.None;
        }

        if (item.Definition == null)
        {
            GD.PrintErr("OpenContainerAction: Item definition is null.");
            return ItemActionResult.None;
        }

        if (item.ContainerInventory == null)
        {
            GD.PrintErr($"OpenContainerAction: '{item.Definition.ItemName}' is not a container.");
            return ItemActionResult.None;
        }

        if (InventoryManager.Instance == null)
        {
            GD.PrintErr("OpenContainerAction: InventoryManager.Instance is null.");
            return ItemActionResult.None;
        }

        if (robinsonGlobals.Instance != null)
        {
            robinsonGlobals.Instance.OpenInventory = true;
            robinsonGlobals.Instance.OpenItemUI = false;
            robinsonGlobals.Instance.CurrentInventoryType = robinsonGlobals.InventoryType.player_inv_and_container;
            robinsonGlobals.Instance.OpenContainerItem = item;
            robinsonGlobals.Instance.CanMove = false;
        }

        Input.MouseMode = Input.MouseModeEnum.Visible;

        // Opens normal inventory UI.
        // Passing player is only used as a non-null marker so HUD shows the container side.
        InventoryManager.Instance.OpenWithContainer(player);

        ContainerInventoryPanel containerPanel =
            player.GetTree().GetFirstNodeInGroup(ContainerPanelGroupName) as ContainerInventoryPanel;

        if (containerPanel == null)
        {
            GD.PrintErr($"OpenContainerAction: No ContainerInventoryPanel found in group '{ContainerPanelGroupName}'.");
            return ItemActionResult.None;
        }

        // This tells the container UI what contents to display.
        containerPanel.OpenContainer(item.ContainerInventory);

        GD.Print($"OpenContainerAction: Opened inventory + container item: {item.Definition.ItemName}");

        return ItemActionResult.RefreshPrompt;
    }
}
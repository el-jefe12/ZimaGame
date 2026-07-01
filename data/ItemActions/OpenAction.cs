using Godot;

[GlobalClass]
public partial class OpenAction : ItemAction
{
    [Export] public PackedScene OpenScene;

    public override ItemActionResult Execute(Node player, ItemInstance item)
    {
        if (OpenScene == null)
        {
            GD.PrintErr("OpenAction: OpenScene is not assigned.");
            return ItemActionResult.None;
        }

        if (player == null)
        {
            GD.PrintErr("OpenAction: Player is null.");
            return ItemActionResult.None;
        }

        SceneTree tree = player.GetTree();

        if (tree == null)
        {
            GD.PrintErr("OpenAction: SceneTree is null.");
            return ItemActionResult.None;
        }

        if (tree.CurrentScene == null)
        {
            GD.PrintErr("OpenAction: CurrentScene is null.");
            return ItemActionResult.None;
        }

        if (robinsonGlobals.Instance != null)
        {
            robinsonGlobals.Instance.OpenItemUI = true;
            robinsonGlobals.Instance.OpenInventory = false;
            robinsonGlobals.Instance.CanMove = false;
        }

        Input.MouseMode = Input.MouseModeEnum.Visible;

        Node overlayInstance = OpenScene.Instantiate();
        tree.CurrentScene.AddChild(overlayInstance);

        GD.Print($"OpenAction: Instantiated {overlayInstance.Name}, type {overlayInstance.GetType().Name}");

        if (overlayInstance is CanvasItem canvasItem)
            canvasItem.Visible = true;

        if (overlayInstance is IItemOpenableUI openableUi)
        {
            openableUi.OpenFromItem(player, item);
        }
        else
        {
            GD.Print($"OpenAction: {overlayInstance.Name} has no IItemOpenableUI. Opened as plain overlay.");
        }

        string itemName = item?.Definition?.ItemName ?? "Unknown item";
        GD.Print($"OpenAction: {itemName} opened UI overlay.");

        return ItemActionResult.RefreshPrompt;
    }
}
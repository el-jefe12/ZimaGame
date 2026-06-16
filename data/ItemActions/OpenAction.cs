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
            Input.MouseMode = Input.MouseModeEnum.Visible;

            GD.Print("OpenAction: Item UI opened. Inventory blocked. Player movement disabled.");
        }

    //    if (robinsonGlobals.Instance != null)
     //   {
     //       robinsonGlobals.Instance.OpenItemUI = false;
    //        robinsonGlobals.Instance.CanMove = true;
     //       Input.MouseMode = Input.MouseModeEnum.Captured;
    //    }

        // Create the UI overlay from the assigned PackedScene.
        Node overlayInstance = OpenScene.Instantiate();

        // Add it to the active scene so it appears on screen.
        tree.CurrentScene.AddChild(overlayInstance);

        string itemName = item?.Definition?.ItemName ?? "Unknown item";
        GD.Print($"{itemName} opened UI overlay.");

        // Item stays in the hotbar, but the prompt can refresh afterward.
        return ItemActionResult.RefreshPrompt;
    }
}
using Godot;

[GlobalClass]
public partial class OpenMenuPanelAction : MenuButtonAction
{
    [Export] public NodePath PanelPath = "";

    public override void Execute(Node sourceButton, MainMenu mainMenu)
    {
        if (mainMenu == null)
        {
            GD.PrintErr("OpenMenuPanelAction: MainMenu is null.");
            return;
        }

        if (PanelPath.IsEmpty)
        {
            GD.PrintErr("OpenMenuPanelAction: PanelPath is empty.");
            return;
        }

        Control? panel = mainMenu.GetNodeOrNull<Control>(PanelPath);

        if (panel == null)
        {
            GD.PrintErr($"OpenMenuPanelAction: Could not find panel at path: {PanelPath}");
            return;
        }

        mainMenu.ShowOnlyPanel(panel);
    }
}
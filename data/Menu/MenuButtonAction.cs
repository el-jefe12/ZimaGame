using Godot;

[GlobalClass]
public abstract partial class MenuButtonAction : Resource
{
    [Export] public string ActionName = "Menu Action";

    // Called when the menu button is activated.
    public abstract void Execute(Node sourceButton, MainMenu mainMenu);
}
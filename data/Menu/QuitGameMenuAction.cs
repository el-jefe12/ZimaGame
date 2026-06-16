using Godot;

[GlobalClass]
public partial class QuitGameMenuAction : MenuButtonAction
{
	public override void Execute(Node sourceButton, MainMenu mainMenu)
	{
		sourceButton.GetTree().Quit();
	}
}

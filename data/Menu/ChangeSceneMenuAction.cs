using Godot;

[GlobalClass]
public partial class ChangeSceneMenuAction : MenuButtonAction
{
	[ExportCategory("Target Scene")]
	[Export(PropertyHint.File, "*.tscn")]
	public string ScenePath = "";

	[ExportCategory("Loading Screen")]
	[Export]
	public bool UseLoadingScreen = true;

	[Export(PropertyHint.File, "*.tscn")]
	public string LoadingScreenPath = "";

	public override void Execute(Node sourceButton, MainMenu mainMenu)
	{
		if (string.IsNullOrWhiteSpace(ScenePath))
		{
			GD.PrintErr("ChangeSceneMenuAction: ScenePath is empty.");
			return;
		}

		// Important when this action is executed from the pause menu.
		PrepareTreeForSceneChange(sourceButton, mainMenu);

		if (!UseLoadingScreen)
		{
			ChangeSceneDirectly(sourceButton);
			return;
		}

		if (string.IsNullOrWhiteSpace(LoadingScreenPath))
		{
			GD.PrintErr("ChangeSceneMenuAction: LoadingScreenPath is empty. Falling back to direct scene change.");
			ChangeSceneDirectly(sourceButton);
			return;
		}

		ChangeToLoadingScreen(sourceButton);
	}

	private void PrepareTreeForSceneChange(Node sourceButton, MainMenu mainMenu)
	{
		SceneTree tree = sourceButton.GetTree();

		// The loading screen/world must not start while the tree is paused.
		tree.Paused = false;

		// If this action was clicked from the pause overlay, close it before scene change.
		// This emits MenuClosed, so PauseMenuManager can free the PauseMenuLayer.
		if (mainMenu.MenuMode == MainMenu.MainMenuMode.PauseMenu)
		{
			mainMenu.CloseMenu();
		}

		// Loading screen/menu should usually have visible mouse.
		Input.MouseMode = Input.MouseModeEnum.Visible;
	}

	private void ChangeSceneDirectly(Node sourceButton)
	{
		SceneTree tree = sourceButton.GetTree();

		tree.Paused = false;

		Error error = tree.ChangeSceneToFile(ScenePath);

		if (error != Error.Ok)
		{
			GD.PrintErr($"ChangeSceneMenuAction: Failed to change scene to {ScenePath}. Error: {error}");
		}
	}

	private void ChangeToLoadingScreen(Node sourceButton)
	{
		SceneTree tree = sourceButton.GetTree();

		tree.Paused = false;

		PackedScene? loadingPackedScene = GD.Load<PackedScene>(LoadingScreenPath);

		if (loadingPackedScene == null)
		{
			GD.PrintErr($"ChangeSceneMenuAction: Could not load loading screen scene: {LoadingScreenPath}");
			ChangeSceneDirectly(sourceButton);
			return;
		}

		Node loadingInstance = loadingPackedScene.Instantiate();

		// This assumes loadingScreenLogic is attached to the root of the loading screen scene.
		loadingScreenLogic? loadingLogic = loadingInstance as loadingScreenLogic;

		if (loadingLogic == null)
		{
			GD.PrintErr("ChangeSceneMenuAction: Loading screen root does not have loadingScreenLogic attached.");
			loadingInstance.QueueFree();
			ChangeSceneDirectly(sourceButton);
			return;
		}

		// Set the real target scene before the loading screen enters the tree.
		loadingLogic.TargetScenePath = ScenePath;

		Node? currentScene = tree.CurrentScene;

		tree.Root.AddChild(loadingInstance);
		tree.CurrentScene = loadingInstance;

		if (currentScene != null)
		{
			currentScene.QueueFree();
		}
	}
}
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

    private void ChangeSceneDirectly(Node sourceButton)
    {
        Error error = sourceButton.GetTree().ChangeSceneToFile(ScenePath);

        if (error != Error.Ok)
        {
            GD.PrintErr($"ChangeSceneMenuAction: Failed to change scene to {ScenePath}. Error: {error}");
        }
    }

    private void ChangeToLoadingScreen(Node sourceButton)
    {
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

        SceneTree tree = sourceButton.GetTree();
        Node? currentScene = tree.CurrentScene;

        tree.Root.AddChild(loadingInstance);
        tree.CurrentScene = loadingInstance;

        if (currentScene != null)
        {
            currentScene.QueueFree();
        }
    }
}
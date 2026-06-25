using Godot;

public partial class PauseMenuManager : Node
{
	public static PauseMenuManager? Instance { get; private set; }

	private const string MainMenuScenePath = "res://scenes/UI/new_title_screen.tscn";

	private MainMenu? _currentPauseMenu;
	private CanvasLayer? _currentPauseMenuLayer;

	public override void _EnterTree()
	{
		Instance = this;
		ProcessMode = ProcessModeEnum.Always;
	}

	public override void _ExitTree()
	{
		if (Instance == this)
		{
			Instance = null;
		}
	}

	public void TogglePauseMenu()
	{
		if (_currentPauseMenu != null && IsInstanceValid(_currentPauseMenu))
		{
			_currentPauseMenu.CloseMenu();
			return;
		}

		OpenPauseMenu();
	}

	public void OpenPauseMenu()
	{
		if (_currentPauseMenu != null && IsInstanceValid(_currentPauseMenu))
			return;

		PackedScene? menuScene = GD.Load<PackedScene>(MainMenuScenePath);

		if (menuScene == null)
		{
			GD.PushError($"PauseMenuManager: Could not load menu scene at path: {MainMenuScenePath}");
			return;
		}

		MainMenu? menu = menuScene.Instantiate<MainMenu>();

		if (menu == null)
		{
			GD.PushError("PauseMenuManager: MainMenu.tscn root node does not have the MainMenu script.");
			return;
		}

		menu.MenuMode = MainMenu.MainMenuMode.PauseMenu;
		menu.MenuClosed += OnPauseMenuClosed;

		CanvasLayer layer = new CanvasLayer();
		layer.Name = "PauseMenuLayer";
		layer.Layer = 100;
		layer.ProcessMode = ProcessModeEnum.Always;

		GetTree().Root.AddChild(layer);
		layer.AddChild(menu);

		_currentPauseMenu = menu;
		_currentPauseMenuLayer = layer;
	}

	private void OnPauseMenuClosed()
	{
		if (_currentPauseMenu != null && IsInstanceValid(_currentPauseMenu))
		{
			_currentPauseMenu.MenuClosed -= OnPauseMenuClosed;
		}

		if (_currentPauseMenuLayer != null && IsInstanceValid(_currentPauseMenuLayer))
		{
			_currentPauseMenuLayer.QueueFree();
		}

		_currentPauseMenu = null;
		_currentPauseMenuLayer = null;

		GetTree().Paused = false;
		Input.MouseMode = Input.MouseModeEnum.Captured;
	}
}
using Godot;
using System.Collections.Generic;

public partial class MainMenu : Control
{
	public enum MainMenuMode
	{
		MainMenu = 0,
		PauseMenu = 1
	}

	private static bool _forceNextInstanceAsMainMenu = false;

	public static void ForceNextInstanceAsMainMenu()
	{
		_forceNextInstanceAsMainMenu = true;
	}

	private static bool ConsumeForceNextInstanceAsMainMenu()
	{
		bool value = _forceNextInstanceAsMainMenu;
		_forceNextInstanceAsMainMenu = false;
		return value;
	}

	[Signal]
	public delegate void MenuClosedEventHandler();

	[ExportCategory("Mode")]
	[Export] public MainMenuMode MenuMode = MainMenuMode.MainMenu;

	[ExportCategory("Menu Pages")]
	[Export] public Godot.Collections.Array<NodePath> MenuPagePaths { get; set; } = new();

	[Export] public int StartingPageIndex = 0;

	[ExportCategory("Mouse")]
	[Export] public bool ForceMouseVisibleWhenOpen = true;

	private readonly List<Control> _menuPages = new();

	private bool _isOpen = false;

	public override void _EnterTree()
	{
		ProcessMode = ProcessModeEnum.Always;
	}

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;

		SetProcessModeAlwaysRecursive(this);
		CachePages();

		// Death screen can force the next loaded menu to behave as a true main menu.
		if (ConsumeForceNextInstanceAsMainMenu())
		{
			MenuMode = MainMenuMode.MainMenu;
			GD.Print("MainMenu: Forced into MainMenu mode by death screen.");
		}

		if (_menuPages.Count > 0)
		{
			int safeIndex = Mathf.Clamp(StartingPageIndex, 0, _menuPages.Count - 1);
			ShowOnlyPanel(_menuPages[safeIndex]);
		}

		if (MenuMode == MainMenuMode.MainMenu)
		{
			GetTree().Paused = false;

			if (robinsonGlobals.Instance != null)
			{
				robinsonGlobals.Instance.CanMove = false;
				robinsonGlobals.Instance.OpenInventory = false;
				robinsonGlobals.Instance.OpenItemUI = false;
				robinsonGlobals.Instance.FreeMouseCursor = true;
			}
		}

		OpenMenu();

		GD.Print($"MainMenu: Ready. Current mode = {MenuMode}");
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (MenuMode != MainMenuMode.PauseMenu)
		{
			return;
		}

		if (@event.IsActionPressed("ui_cancel"))
		{
			CloseMenu();
			GetViewport().SetInputAsHandled();
		}
	}

	public override void _Process(double delta)
	{
		if (!_isOpen)
		{
			return;
		}

		if (ForceMouseVisibleWhenOpen && Input.MouseMode != Input.MouseModeEnum.Visible)
		{
			MakeMouseUsable();
		}
	}

	private void SetProcessModeAlwaysRecursive(Node node)
	{
		node.ProcessMode = ProcessModeEnum.Always;

		foreach (Node child in node.GetChildren())
		{
			SetProcessModeAlwaysRecursive(child);
		}
	}

	public void OpenMenu()
	{
		_isOpen = true;
		Visible = true;

		MakeMouseUsable();

		if (MenuMode == MainMenuMode.PauseMenu)
		{
			GetTree().Paused = true;
		}
		else
		{
			GetTree().Paused = false;
		}
	}

	public void CloseMenu()
	{
		if (MenuMode == MainMenuMode.MainMenu)
		{
			GD.Print("MainMenu: CloseMenu ignored because this is MainMenu mode.");
			return;
		}

		_isOpen = false;
		Visible = false;

		if (MenuMode == MainMenuMode.PauseMenu)
		{
			GetTree().Paused = false;
			Input.MouseMode = Input.MouseModeEnum.Captured;

			if (robinsonGlobals.Instance != null)
			{
				robinsonGlobals.Instance.FreeMouseCursor = false;
			}
		}

		EmitSignal(SignalName.MenuClosed);
	}

	public void ResumeGame()
	{
		if (MenuMode != MainMenuMode.PauseMenu)
		{
			return;
		}

		CloseMenu();
	}

	public void QuitGame()
	{
		GetTree().Paused = false;
		GetTree().Quit();
	}

	public void MakeMouseUsable()
	{
		Input.MouseMode = Input.MouseModeEnum.Visible;

		if (robinsonGlobals.Instance != null)
		{
			robinsonGlobals.Instance.FreeMouseCursor = true;
		}
	}

	private void CachePages()
	{
		_menuPages.Clear();

		foreach (NodePath pagePath in MenuPagePaths)
		{
			Control? page = GetNodeOrNull<Control>(pagePath);

			if (page == null)
			{
				GD.PrintErr($"MainMenu: Could not find menu page at path: {pagePath}");
				continue;
			}

			_menuPages.Add(page);
		}
	}

	public void ShowOnlyPanel(Control panelToShow)
	{
		foreach (Control page in _menuPages)
		{
			page.Visible = page == panelToShow;
		}
	}

	public void HideAllPanels()
	{
		foreach (Control page in _menuPages)
		{
			page.Visible = false;
		}
	}
}
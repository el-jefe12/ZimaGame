using Godot;
using System;

public partial class DeathScreenLogic : Control
{
	[ExportCategory("Fade")]
	[Export] public float BackgroundFadeDuration = 0.45f;

	[Export] public float TitleDelayAfterBlack = 0.05f;
	[Export] public float TitleFadeDuration = 0.12f;

	[Export] public float DetailsDelayAfterTitle = 0.10f;
	[Export] public float DetailsFadeDuration = 0.22f;

	[ExportCategory("Input Actions")]
	[Export] public string RestartInputAction = "restart_game";
	[Export] public string MainMenuInputAction = "main_menu";

	[ExportCategory("Scenes")]
	[Export(PropertyHint.File, "*.tscn")]
	public string RestartScenePath = "";

	[Export(PropertyHint.File, "*.tscn")]
	public string MainMenuScenePath = "";

	[ExportCategory("Loading Screen")]
	[Export] public bool UseLoadingScreenForRestart = true;
	[Export] public bool UseLoadingScreenForMainMenu = false;

	[Export(PropertyHint.File, "*.tscn")]
	public string LoadingScreenScenePath = "";

	[ExportCategory("Input Timing")]
	[Export] public bool AllowInputOnlyAfterFade = true;

	private ColorRect _colorBackground = null!;
	private Control _buttonsContainer = null!;

	private RichTextLabel _deathLabel = null!;
	private RichTextLabel _deathCauseLabel = null!;
	private RichTextLabel _daysSurvivedLabel = null!;

	private bool _inputAllowed = false;
	private bool _sceneChanging = false;

	public override void _Ready()
	{
		_colorBackground = GetNode<ColorRect>("ColorBackground");

		_deathLabel = GetNode<RichTextLabel>("%DeathLabel");
		_deathCauseLabel = GetNode<RichTextLabel>("%DeathCauseLabel");
		_daysSurvivedLabel = GetNode<RichTextLabel>("%DaysSurvivedLabel");

		// Your ButtonsContainer might be VBoxContainer; use Control so it is flexible.
		_buttonsContainer = GetNode<Control>("%ButtonsContainer");

		// This allows the death screen to listen for keyboard/controller input.
		SetProcessUnhandledInput(true);

		// Initial state: background transparent, UI invisible.
		SetAlpha(_colorBackground, 0.0f);
		SetAlpha(_deathLabel, 0.0f);
		SetAlpha(_deathCauseLabel, 0.0f);
		SetAlpha(_daysSurvivedLabel, 0.0f);
		SetAlpha(_buttonsContainer, 0.0f);

		// If disabled, inputs work immediately.
		_inputAllowed = !AllowInputOnlyAfterFade;

		PlayDeathSequence();
	}

	public override void _UnhandledInput(InputEvent inputEvent)
	{
		if (_sceneChanging)
		{
			return;
		}

		if (!_inputAllowed)
		{
			return;
		}

		// Restart game input.
		if (!string.IsNullOrWhiteSpace(RestartInputAction) && inputEvent.IsActionPressed(RestartInputAction))
		{
			GetViewport().SetInputAsHandled();
			RestartGame();
			return;
		}

		// Main menu input.
		if (!string.IsNullOrWhiteSpace(MainMenuInputAction) && inputEvent.IsActionPressed(MainMenuInputAction))
		{
			GetViewport().SetInputAsHandled();
			GoToMainMenu();
			return;
		}
	}

	private void PlayDeathSequence()
	{
		Tween tween = CreateTween();
		tween.SetTrans(Tween.TransitionType.Sine);
		tween.SetEase(Tween.EaseType.Out);

		// 1) Fade to black.
		tween.TweenProperty(_colorBackground, "modulate:a", 1.0f, BackgroundFadeDuration);

		// 2) DeathLabel quickly.
		tween.TweenInterval(TitleDelayAfterBlack);
		tween.TweenProperty(_deathLabel, "modulate:a", 1.0f, TitleFadeDuration);

		// 3) Cause + days + buttons together.
		tween.TweenInterval(DetailsDelayAfterTitle);
		tween.TweenProperty(_deathCauseLabel, "modulate:a", 1.0f, DetailsFadeDuration);
		tween.Parallel().TweenProperty(_daysSurvivedLabel, "modulate:a", 1.0f, DetailsFadeDuration);
		tween.Parallel().TweenProperty(_buttonsContainer, "modulate:a", 1.0f, DetailsFadeDuration);

		// Allow keyboard/controller input after the death screen has appeared.
		tween.Finished += OnDeathSequenceFinished;
	}

	private void OnDeathSequenceFinished()
	{
		_inputAllowed = true;
	}

	private void RestartGame()
	{
		_sceneChanging = true;

		string targetScenePath = GetRestartTargetScenePath();

		if (string.IsNullOrWhiteSpace(targetScenePath))
		{
			_sceneChanging = false;
			GD.PushError("DeathScreenLogic: Could not restart. RestartScenePath is empty and the current scene has no SceneFilePath.");
			return;
		}

		PrepareForLeavingDeathScreen();

		ChangeScene(targetScenePath, UseLoadingScreenForRestart);
	}

	private void GoToMainMenu()
	{
		if (string.IsNullOrWhiteSpace(MainMenuScenePath))
		{
			GD.PushError("DeathScreenLogic: MainMenuScenePath is empty.");
			return;
		}

		_sceneChanging = true;

		// This is the important part:
		// The next MainMenu instance will force itself into MainMenu mode.
		MainMenu.ForceNextInstanceAsMainMenu();

		if (robinsonGlobals.Instance != null)
		{
			robinsonGlobals.Instance.CanMove = false;
			robinsonGlobals.Instance.OpenInventory = false;
			robinsonGlobals.Instance.OpenItemUI = false;
			robinsonGlobals.Instance.FreeMouseCursor = true;
		}

		PrepareForLeavingDeathScreen();

		ChangeScene(MainMenuScenePath, UseLoadingScreenForMainMenu);
	}

	private void PrepareForLeavingDeathScreen()
	{
		// Stop this overlay from eating input while the scene changes.
		_inputAllowed = false;
		SetProcessUnhandledInput(false);

		// Make it disappear immediately, even before QueueFree finishes.
		Hide();

		// Reset global player/game state that death changed.
		if (robinsonGlobals.Instance != null)
		{
			robinsonGlobals.Instance.CanMove = true;
			robinsonGlobals.Instance.FreeMouseCursor = false;
			robinsonGlobals.Instance.OpenInventory = false;
			robinsonGlobals.Instance.OpenItemUI = false;
		}

		// Usually restart should capture mouse again.
		// Main menu may set it visible again in its own _Ready().
		Input.MouseMode = Input.MouseModeEnum.Captured;

		// Remove the death screen overlay itself.
		QueueFree();
	}

	private string GetRestartTargetScenePath()
	{
		// If you manually set a restart scene, use that.
		if (!string.IsNullOrWhiteSpace(RestartScenePath))
		{
			return RestartScenePath;
		}

		// Otherwise reload the currently active scene file.
		Node? currentScene = GetTree().CurrentScene;

		if (currentScene == null)
		{
			return "";
		}

		return currentScene.SceneFilePath;
	}

	private void ChangeScene(string targetScenePath, bool useLoadingScreen)
	{
		if (useLoadingScreen)
		{
			ChangeToLoadingScreen(targetScenePath);
			return;
		}

		ChangeSceneDirectly(targetScenePath);
	}

	private void ChangeSceneDirectly(string targetScenePath)
	{
		Error error = GetTree().ChangeSceneToFile(targetScenePath);

		if (error != Error.Ok)
		{
			_sceneChanging = false;
			GD.PushError($"DeathScreenLogic: Failed to change scene to '{targetScenePath}'. Error: {error}");
		}
	}

	private void ChangeToLoadingScreen(string targetScenePath)
	{
		if (string.IsNullOrWhiteSpace(LoadingScreenScenePath))
		{
			GD.PushError("DeathScreenLogic: LoadingScreenScenePath is empty. Falling back to direct scene load.");
			ChangeSceneDirectly(targetScenePath);
			return;
		}

		PackedScene? loadingScreenScene = GD.Load<PackedScene>(LoadingScreenScenePath);

		if (loadingScreenScene == null)
		{
			_sceneChanging = false;
			GD.PushError($"DeathScreenLogic: Could not load loading screen scene at '{LoadingScreenScenePath}'.");
			return;
		}

		Node loadingScreenInstance = loadingScreenScene.Instantiate();

		// This sends the final target scene to your loading screen script.
		// Your loading screen script needs: [Export] public string TargetScenePath = "";
		loadingScreenInstance.Set("TargetScenePath", targetScenePath);

		SceneTree tree = GetTree();
		Node? oldCurrentScene = tree.CurrentScene;

		tree.Root.AddChild(loadingScreenInstance);
		tree.CurrentScene = loadingScreenInstance;

		// Remove the old world/menu scene after the loading screen is active.
		oldCurrentScene?.QueueFree();
	}

	private static void SetAlpha(CanvasItem item, float alpha)
	{
		Color color = item.Modulate;
		color.A = Mathf.Clamp(alpha, 0.0f, 1.0f);
		item.Modulate = color;
	}
}
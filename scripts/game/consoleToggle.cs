using Godot;
using System;

public partial class consoleToggle : Node
{
    private const string DevConsoleScenePath = "res://scenes/dev/console.tscn";
    private const string ToggleAction = "game_open_console";

    [Export(PropertyHint.Range, "0.01,1.0,0.01")]
    public double SlideDurationSeconds { get; set; } = 0.12;

    [Export(PropertyHint.Range, "0,2000,1")]
    public float FallbackConsoleHeight { get; set; } = 320.0f;

    // If true: show cursor when console is open
    [Export] public bool LockMouseWhileOpen { get; set; } = true;

    // If true: prevent player movement while console is open
    [Export] public bool LockMovementWhileOpen { get; set; } = true;

    private PackedScene _devConsoleScene;
    private CanvasLayer _layer;
    private Control _consoleUi;
    private Tween _tween;

    private bool _isBuilt = false;
    private float _consoleHeight = 0.0f;

    public override void _Ready()
    {
        // Autoload Nodes may not receive input unless you enable it.
        SetProcessInput(true);

        _devConsoleScene = GD.Load<PackedScene>(DevConsoleScenePath);
        if (_devConsoleScene == null)
        {
            GD.PushError($"[console] Failed to load: {DevConsoleScenePath}");
            return;
        }

        CallDeferred(nameof(BuildConsoleUiDeferred));
        GD.Print("[console] Autoload ready.");
    }

    private async void BuildConsoleUiDeferred()
    {
        if (_isBuilt)
            return;

        _layer = new CanvasLayer { Layer = 50 };
        GetTree().Root.AddChild(_layer);

        _consoleUi = _devConsoleScene.Instantiate<Control>();
        _layer.AddChild(_consoleUi);

        // IMPORTANT: do NOT force anchors/presets here.
        // Let console.tscn control its own size (full size as authored).
        _consoleUi.Visible = false;

        // Wait a frame so layouts apply and sizes become valid
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        _consoleHeight = ComputeConsoleHeight();
        _consoleUi.Position = new Vector2(_consoleUi.Position.X, -_consoleHeight);

        // Sync globals to "closed" state on boot
        robinsonGlobals.Instance.ConsoleActive = false;
        ApplyGlobalState();

        _isBuilt = true;
        GD.Print("[console] Console UI spawned.");
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && !key.Echo && @event.IsActionPressed(ToggleAction))
        {
            ToggleConsole();
            // If you truly want to stop the toggle key from reaching gameplay, uncomment:
            // GetViewport().SetInputAsHandled();
        }
    }

    private void ToggleConsole()
    {
        if (!_isBuilt || _consoleUi == null)
        {
            GD.Print("[console] Toggle ignored: UI not built yet.");
            return;
        }

        robinsonGlobals.Instance.ConsoleActive = !robinsonGlobals.Instance.ConsoleActive;
        ApplyGlobalState();

        if (robinsonGlobals.Instance.ConsoleActive)
            ShowWithSlide();
        else
            HideWithSlide();
    }

    private void ApplyGlobalState()
    {
        bool isOpen = robinsonGlobals.Instance.ConsoleActive;

        if (LockMouseWhileOpen)
            Input.MouseMode = isOpen ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Captured;

        if (LockMovementWhileOpen)
            robinsonGlobals.Instance.CanMove = !isOpen;
    }

    private float ComputeConsoleHeight()
    {
        if (_consoleUi == null)
            return FallbackConsoleHeight;

        // Prefer actual size; fallback to minimum size; fallback to exported value.
        float hA = _consoleUi.Size.Y;
        float hB = _consoleUi.GetCombinedMinimumSize().Y;
        float h = Mathf.Max(hA, hB);

        if (h <= 1.0f)
            h = FallbackConsoleHeight;

        return h;
    }

    private void ShowWithSlide()
    {
        _tween?.Kill();

        _consoleHeight = ComputeConsoleHeight();

        _consoleUi.Visible = true;

        // Preserve X (in case your scene positions it), only animate Y.
        float x = _consoleUi.Position.X;
        _consoleUi.Position = new Vector2(x, -_consoleHeight);

        _tween = CreateTween();
        _tween.SetTrans(Tween.TransitionType.Quad);
        _tween.SetEase(Tween.EaseType.Out);
        _tween.TweenProperty(_consoleUi, "position", new Vector2(x, 0.0f), SlideDurationSeconds);
    }

    private void HideWithSlide()
    {
        _tween?.Kill();

        _consoleHeight = ComputeConsoleHeight();

        float x = _consoleUi.Position.X;

        _tween = CreateTween();
        _tween.SetTrans(Tween.TransitionType.Quad);
        _tween.SetEase(Tween.EaseType.In);
        _tween.TweenProperty(_consoleUi, "position", new Vector2(x, -_consoleHeight), SlideDurationSeconds);

        _tween.TweenCallback(Callable.From(() =>
        {
            if (_consoleUi != null)
                _consoleUi.Visible = false;
        }));
    }
}

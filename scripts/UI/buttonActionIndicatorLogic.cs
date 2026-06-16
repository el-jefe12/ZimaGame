using Godot;

[Tool]
public partial class buttonActionIndicatorLogic : PanelContainer
{

    // Visual press feedback
    [Export] public float PressScale = 0.9f;   // how small when pressed
    [Export] public float PressSpeed = 12f;    // animation speed

    private bool _heldRequired = false;

    [Export] public bool HeldRequired
    {
        get => _heldRequired;
        set { _heldRequired = value; ApplyHeldBar(); }
    }

    // Hold interaction timing
    [Export] public float HoldDuration = 1.5f; // seconds required to complete hold

    // How fast the bar falls back when the key is released
    [Export] public float HoldFallSpeed = 4f;

    private float _holdTimer = 0f;

    private bool _holdTriggered = false;

    private TextureRect _ButtonIconPrompt;

    private TextureProgressBar _HeldTextureProgressBar;

    private bool _isPressed = false;



    private string _mainText = "E";
    private string _actionText = "Action Name";
    private bool _isLong = false;

    private StringName _inputActionName = default;
    [Export] public StringName InputActionName
    {
        get => _inputActionName;
        set { _inputActionName = value; if (UseInputMapKey) RefreshFromInputMap(); }
    }

    private bool _useInputMapKey = false;

    [Export]
    public bool UseInputMapKey
    {
        get => _useInputMapKey;
        set
        {
            _useInputMapKey = value;

            if (IsInsideTree())
            {
                if (_useInputMapKey)
                    RefreshFromInputMap();
                else
                {
                    _mainText = _manualMainText;
                    ApplyMain();
                    ApplyButtonBase();
                }
            }
        }
    }

    private string _manualMainText = "E";

    [Export]
    public string ManualMainText
    {
        get => _manualMainText;
        set
        {
            _manualMainText = value ?? "";
            if (!UseInputMapKey)
            {
                _mainText = _manualMainText;
                ApplyMain();
                ApplyButtonBase();
            }
        }
    }

    [Export] public Texture2D? ButtonBase { get; set; }
    [Export] public Texture2D? ButtonBaseLong { get; set; }

    [Export]
    public bool IsLong
    {
        get => _isLong;
        set { _isLong = value; ApplyButtonBase(); }
    }

    private TextureRect? _buttonIconBase;
    private RichTextLabel? _buttonRichTextLabel;
    private RichTextLabel? _buttonActionLabel;

    [Export(PropertyHint.MultilineText)]
    public string ButtonActionRichTextLabel
    {
        get => _actionText;
        set { _actionText = value ?? ""; ApplyAction(); }
    }


    [Signal]
    public delegate void HoldCompletedEventHandler();

    public override void _Ready()
    {
        _buttonIconBase = GetNodeOrNull<TextureRect>("%ButtonIconBase");
        _buttonRichTextLabel = GetNodeOrNull<RichTextLabel>("%ButtonRichTextLabel");
        _buttonActionLabel = GetNodeOrNull<RichTextLabel>("%ButtonActionRichTextLabel");

        _ButtonIconPrompt = GetNodeOrNull<TextureRect>("%ButtonIconPrompt");

        // IMPORTANT: resolve progress bar here
        _HeldTextureProgressBar = GetNodeOrNull<TextureProgressBar>("%HeldTextureProgressBar");

        if (_HeldTextureProgressBar != null)
            _HeldTextureProgressBar.Visible = _heldRequired;

        if (_buttonRichTextLabel != null) _buttonRichTextLabel.BbcodeEnabled = true;
        if (_buttonActionLabel != null) _buttonActionLabel.BbcodeEnabled = true;

        ApplyMain();
        ApplyAction();
        ApplyButtonBase();

        if (UseInputMapKey)
            RefreshFromInputMap();
    }

    public override void _Process(double delta)
    {
        if (_buttonIconBase == null)
            return;

        // Prevent checking invalid InputMap actions
        if (_inputActionName == default || _inputActionName.ToString() == "")
            return;

        bool pressed = Input.IsActionPressed(_inputActionName);

        if (pressed != _isPressed)
            _isPressed = pressed;

        // Target scale animation
        float target = _isPressed ? PressScale : 1.0f;

        Vector2 current = _buttonIconBase.Scale;
        Vector2 targetScale = new Vector2(target, target);

        // Smooth interpolation toward target scale
        _buttonIconBase.Scale = current.Lerp(targetScale, (float)(PressSpeed * delta));



        // =============================
        // HOLD PROGRESS BAR LOGIC
        // =============================

        if (_heldRequired && _HeldTextureProgressBar != null)
        {
            if (pressed)
            {
                _holdTimer += (float)delta;

                if (_holdTimer >= HoldDuration && !_holdTriggered)
                {
                    _holdTimer = HoldDuration;
                    _holdTriggered = true;

                    EmitSignal(SignalName.HoldCompleted);
                }
            }
            else
            {
                _holdTriggered = false;

                _holdTimer -= (float)delta * HoldFallSpeed;

                if (_holdTimer < 0f)
                    _holdTimer = 0f;
            }

            float Tprogress = _holdTimer / HoldDuration;
            _HeldTextureProgressBar.Value = Tprogress * 100f;
        }

        // Update progress bar
        float progress = _holdTimer / HoldDuration;

        _HeldTextureProgressBar.Value = progress * 100f;
    }

    // Use this for your hover popup:
    // - actionName: input map action (e.g. "interact")
    // - actionText: display name (e.g. "Pick up")
    public void SetAction(StringName actionName, string actionText)
    {
        _inputActionName = actionName;
        _actionText = actionText;

        _holdTimer = 0f;
        _holdTriggered = false;

        if (_HeldTextureProgressBar != null)
            _HeldTextureProgressBar.Value = 0f;

        ApplyAction();
        if (UseInputMapKey) RefreshFromInputMap();
    }

    private void ApplyMain()
    {
        if (!IsInsideTree()) return;
        _buttonRichTextLabel ??= GetNodeOrNull<RichTextLabel>("%ButtonRichTextLabel");
        if (_buttonRichTextLabel != null) _buttonRichTextLabel.Text = _mainText;
    }

    private void ApplyAction()
    {
        if (!IsInsideTree()) return;
        _buttonActionLabel ??= GetNodeOrNull<RichTextLabel>("%ButtonActionRichTextLabel");
        if (_buttonActionLabel != null) _buttonActionLabel.Text = _actionText;
    }

    private void ApplyButtonBase()
    {
        if (!IsInsideTree()) return;
        _buttonIconBase ??= GetNodeOrNull<TextureRect>("%ButtonIconBase");
        if (_buttonIconBase == null) return;

        bool longNow = _isLong || (_mainText?.Length > 1);

        if (longNow)
        {
            _buttonIconBase.Texture = ButtonBaseLong;
            _buttonIconBase.CustomMinimumSize = new Vector2(64, 32);
        }
        else
        {
            _buttonIconBase.Texture = ButtonBase;
            _buttonIconBase.CustomMinimumSize = new Vector2(32,32);
        }
    }

    private void ApplyHeldBar()
    {
        // Prevent running before the node is inside the scene tree
        if (!IsInsideTree())
            return;

        // Resolve node if not cached yet
        _HeldTextureProgressBar ??= GetNodeOrNull<TextureProgressBar>("%HeldTextureProgressBar");

        if (_HeldTextureProgressBar == null)
            return;

        _HeldTextureProgressBar.Visible = _heldRequired;
    }

    private void RefreshFromInputMap()
    {
        if (!IsInsideTree()) return;
        if (_inputActionName == default || _inputActionName.ToString() == "")
            return;

        string label = GetFirstBindingLabel(_inputActionName);
        if (label == "?") return;

        _mainText = label;
        ApplyMain();
        ApplyButtonBase();
    }

    private static string GetFirstBindingLabel(StringName actionName)
    {
        foreach (var e in InputMap.ActionGetEvents(actionName))
        {
            if (e is InputEventKey k)
            {
                Key key = k.PhysicalKeycode != Key.None ? k.PhysicalKeycode : k.Keycode;
                string s = OS.GetKeycodeString(key);
                return string.IsNullOrEmpty(s) ? "?" : s;
            }
            if (e is InputEventMouseButton mb)
                return mb.ButtonIndex == MouseButton.Left ? "LMB"
                     : mb.ButtonIndex == MouseButton.Right ? "RMB"
                     : mb.ButtonIndex == MouseButton.Middle ? "MMB"
                     : "Mouse";
            if (e is InputEventJoypadButton jb)
                return $"Pad {jb.ButtonIndex}";
        }
        return "?";
    }
}

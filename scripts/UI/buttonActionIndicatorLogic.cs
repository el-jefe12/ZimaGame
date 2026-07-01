using Godot;

[Tool]
public partial class buttonActionIndicatorLogic : PanelContainer
{
	[Export] public float PressScale = 0.9f;
	[Export] public float PressSpeed = 12f;

	private bool _heldRequired = false;

	[Export]
	public bool HeldRequired
	{
		get => _heldRequired;
		set
		{
			_heldRequired = value;
			ApplyHeldBar();
		}
	}

	[Export] public float HoldDuration = 1.5f;
	[Export] public float HoldFallSpeed = 4f;

	private float _holdTimer = 0f;
	private bool _holdTriggered = false;
	private bool _isPressed = false;

	private string _mainText = "E";
	private string _actionText = "Action Name";
	private bool _isLong = false;

	private StringName _inputActionName = default;

	[Export]
	public StringName InputActionName
	{
		get => _inputActionName;
		set
		{
			_inputActionName = value;

			if (UseInputMapKey)
				RefreshFromInputMap();
		}
	}

	private bool _useInputMapKey = false;

	[Export]
	public bool UseInputMapKey
	{
		get => _useInputMapKey;
		set
		{
			_useInputMapKey = value;

			if (!IsInsideTree())
				return;

			if (_useInputMapKey)
			{
				RefreshFromInputMap();
			}
			else
			{
				_mainText = _manualMainText;
				ApplyMain();
				ApplyButtonBase();
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

	[Export] public Texture2D ButtonBase { get; set; }
	[Export] public Texture2D ButtonBaseLong { get; set; }

	[Export]
	public bool IsLong
	{
		get => _isLong;
		set
		{
			_isLong = value;
			ApplyButtonBase();
		}
	}

	[Export(PropertyHint.MultilineText)]
	public string ButtonActionRichTextLabel
	{
		get => _actionText;
		set
		{
			_actionText = value ?? "";
			ApplyAction();
		}
	}

	private TextureRect _buttonIconBase;
	private RichTextLabel _buttonRichTextLabel;
	private RichTextLabel _buttonActionLabel;
	private TextureProgressBar _heldTextureProgressBar;

	[Signal]
	public delegate void HoldCompletedEventHandler();

	public override void _Ready()
	{
		_buttonIconBase = GetNodeOrNull<TextureRect>("%ButtonIconBase");
		_buttonRichTextLabel = GetNodeOrNull<RichTextLabel>("%ButtonRichTextLabel");
		_buttonActionLabel = GetNodeOrNull<RichTextLabel>("%ButtonActionRichTextLabel");
		_heldTextureProgressBar = GetNodeOrNull<TextureProgressBar>("%HeldTextureProgressBar");

		if (_buttonRichTextLabel != null)
			_buttonRichTextLabel.BbcodeEnabled = true;

		if (_buttonActionLabel != null)
			_buttonActionLabel.BbcodeEnabled = true;

		if (_heldTextureProgressBar != null)
		{
			_heldTextureProgressBar.Visible = _heldRequired;
			_heldTextureProgressBar.Value = 0f;
		}

		ApplyMain();
		ApplyAction();
		ApplyButtonBase();

		if (UseInputMapKey)
			RefreshFromInputMap();
	}

	public override void _Process(double delta)
	{
		if (Engine.IsEditorHint())
			return;

		if (!IsVisibleInTree())
			return;

		if (_buttonIconBase == null)
			return;

		if (_inputActionName == default || string.IsNullOrWhiteSpace(_inputActionName.ToString()))
			return;

		bool pressed = Input.IsActionPressed(_inputActionName);

		_isPressed = pressed;

		float target = _isPressed ? PressScale : 1.0f;

		Vector2 current = _buttonIconBase.Scale;
		Vector2 targetScale = new Vector2(target, target);

		_buttonIconBase.Scale = current.Lerp(targetScale, (float)(PressSpeed * delta));

		if (!_heldRequired)
		{
			_holdTimer = 0f;
			_holdTriggered = false;

			if (_heldTextureProgressBar != null)
				_heldTextureProgressBar.Value = 0f;

			return;
		}

		if (_heldTextureProgressBar == null)
			return;

		if (HoldDuration <= 0f)
			HoldDuration = 0.1f;

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

		float progress = _holdTimer / HoldDuration;
		_heldTextureProgressBar.Value = progress * 100f;
	}

	public void SetAction(StringName actionName, string actionText)
	{
		SetAction(actionName, actionText, _heldRequired, HoldDuration);
	}

	public void SetAction(StringName actionName, string actionText, bool heldRequired, float holdDuration = 1.5f)
	{
		_inputActionName = actionName;
		_actionText = actionText ?? "";

		HeldRequired = heldRequired;
		HoldDuration = holdDuration;

		_holdTimer = 0f;
		_holdTriggered = false;

		if (_heldTextureProgressBar != null)
		{
			_heldTextureProgressBar.Visible = _heldRequired;
			_heldTextureProgressBar.Value = 0f;
		}

		ApplyAction();

		if (UseInputMapKey)
			RefreshFromInputMap();
	}

	private void ApplyMain()
	{
		if (!IsInsideTree())
			return;

		_buttonRichTextLabel ??= GetNodeOrNull<RichTextLabel>("%ButtonRichTextLabel");

		if (_buttonRichTextLabel != null)
			_buttonRichTextLabel.Text = _mainText;
	}

	private void ApplyAction()
	{
		if (!IsInsideTree())
			return;

		_buttonActionLabel ??= GetNodeOrNull<RichTextLabel>("%ButtonActionRichTextLabel");

		if (_buttonActionLabel != null)
			_buttonActionLabel.Text = _actionText;
	}

	private void ApplyButtonBase()
	{
		if (!IsInsideTree())
			return;

		_buttonIconBase ??= GetNodeOrNull<TextureRect>("%ButtonIconBase");

		if (_buttonIconBase == null)
			return;

		bool longNow = _isLong || (_mainText != null && _mainText.Length > 1);

		if (longNow)
		{
			_buttonIconBase.Texture = ButtonBaseLong;
			_buttonIconBase.CustomMinimumSize = new Vector2(64, 32);
		}
		else
		{
			_buttonIconBase.Texture = ButtonBase;
			_buttonIconBase.CustomMinimumSize = new Vector2(32, 32);
		}
	}

	private void ApplyHeldBar()
	{
		if (!IsInsideTree())
			return;

		_heldTextureProgressBar ??= GetNodeOrNull<TextureProgressBar>("%HeldTextureProgressBar");

		if (_heldTextureProgressBar == null)
			return;

		_heldTextureProgressBar.Visible = _heldRequired;

		if (!_heldRequired)
			_heldTextureProgressBar.Value = 0f;
	}

	private void RefreshFromInputMap()
	{
		if (!IsInsideTree())
			return;

		if (_inputActionName == default || string.IsNullOrWhiteSpace(_inputActionName.ToString()))
			return;

		string label = GetFirstBindingLabel(_inputActionName);

		if (label == "?")
			return;

		_mainText = label;

		ApplyMain();
		ApplyButtonBase();
	}

	private static string GetFirstBindingLabel(StringName actionName)
	{
		foreach (InputEvent inputEvent in InputMap.ActionGetEvents(actionName))
		{
			if (inputEvent is InputEventKey keyEvent)
			{
				Key key = keyEvent.PhysicalKeycode != Key.None
					? keyEvent.PhysicalKeycode
					: keyEvent.Keycode;

				return GetKeyLabel(key);
			}

			if (inputEvent is InputEventMouseButton mouseButton)
			{
				return mouseButton.ButtonIndex switch
				{
					MouseButton.Left => "LMB",
					MouseButton.Right => "RMB",
					MouseButton.Middle => "MMB",
					MouseButton.WheelUp => "Wheel Up",
					MouseButton.WheelDown => "Wheel Down",
					MouseButton.Xbutton1 => "Mouse 4",
					MouseButton.Xbutton2 => "Mouse 5",
					_ => "Mouse"
				};
			}

			if (inputEvent is InputEventJoypadButton joypadButton)
			{
				//return GetJoypadButtonLabel(joypadButton.ButtonIndex);
			}

			if (inputEvent is InputEventJoypadMotion joypadMotion)
			{
				//return GetJoypadMotionLabel(joypadMotion.Axis, joypadMotion.AxisValue);
			}
		}

		return "?";
	}

	private static string GetKeyLabel(Key key)
	{
		return key switch
		{
			Key.Backspace => "←",
			Key.Tab => "Tab",
			Key.Enter => "Enter",
			Key.KpEnter => "Numpad Enter",
			Key.Escape => "Esc",
			Key.Space => "Space",

			Key.Left => "←",
			Key.Right => "→",
			Key.Up => "↑",
			Key.Down => "↓",

			Key.Shift => "Shift",
			Key.Ctrl => "Ctrl",
			Key.Alt => "Alt",
			Key.Meta => "Meta",

			Key.Insert => "Insert",
			Key.Delete => "Delete",
			Key.Home => "Home",
			Key.End => "End",
			Key.Pageup => "Page Up",
			Key.Pagedown => "Page Down",

			Key.Capslock => "Caps",
			Key.Numlock => "Num",
			Key.Scrolllock => "Scroll",

			Key.F1 => "F1",
			Key.F2 => "F2",
			Key.F3 => "F3",
			Key.F4 => "F4",
			Key.F5 => "F5",
			Key.F6 => "F6",
			Key.F7 => "F7",
			Key.F8 => "F8",
			Key.F9 => "F9",
			Key.F10 => "F10",
			Key.F11 => "F11",
			Key.F12 => "F12",

			Key.Kp0 => "Num 0",
			Key.Kp1 => "Num 1",
			Key.Kp2 => "Num 2",
			Key.Kp3 => "Num 3",
			Key.Kp4 => "Num 4",
			Key.Kp5 => "Num 5",
			Key.Kp6 => "Num 6",
			Key.Kp7 => "Num 7",
			Key.Kp8 => "Num 8",
			Key.Kp9 => "Num 9",
			Key.KpAdd => "Num +",
			Key.KpSubtract => "Num -",
			Key.KpMultiply => "Num *",
			Key.KpDivide => "Num /",
			Key.KpPeriod => "Num .",

			_ => GetFallbackKeyLabel(key)
		};
	}

	private static string GetFallbackKeyLabel(Key key)
	{
		string keyText = OS.GetKeycodeString(key);

		if (string.IsNullOrEmpty(keyText))
			return "?";

		if (keyText.Length == 1)
			return keyText.ToUpper();

		return keyText;
	}
}

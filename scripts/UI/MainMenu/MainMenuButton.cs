using Godot;

[Tool]
public partial class MainMenuButton : PanelContainer
{
	public enum MenuButtonState
	{
		Normal = 0,
		Disabled = 1,
		Hidden = 2
	}

	[ExportCategory("Button Setup")]
	[Export]
	public Resource? ActionResource
	{
		get => _actionResource;
		set
		{
			_actionResource = value;
			ApplyEditorUpdate();
		}
	}

	[Export]
	public NodePath TextureButtonPath
	{
		get => _textureButtonPath;
		set
		{
			_textureButtonPath = value;
			ApplyEditorUpdate();
		}
	}

	[Export]
	public NodePath LabelPath
	{
		get => _labelPath;
		set
		{
			_labelPath = value;
			ApplyEditorUpdate();
		}
	}

	[ExportCategory("Text")]
	[Export]
	public string ButtonText
	{
		get => _buttonText;
		set
		{
			_buttonText = value;
			ApplyEditorUpdate();
		}
	}

	[ExportCategory("State")]
	[Export]
	public MenuButtonState State
	{
		get => _state;
		set
		{
			_state = value;
			ApplyEditorUpdate();
		}
	}

	[Export]
	public bool UseDisabledModulate
	{
		get => _useDisabledModulate;
		set
		{
			_useDisabledModulate = value;
			ApplyEditorUpdate();
		}
	}

	[Export]
	public float DisabledAlpha
	{
		get => _disabledAlpha;
		set
		{
			_disabledAlpha = Mathf.Clamp(value, 0.0f, 1.0f);
			ApplyEditorUpdate();
		}
	}

	private Resource? _actionResource;

	private NodePath _textureButtonPath = "StarVBoxContainer/StarButtonTexture";
	private NodePath _labelPath = "StarVBoxContainer/StarLabel";

	private string _buttonText = "Button";
	private MenuButtonState _state = MenuButtonState.Normal;

	private bool _useDisabledModulate = true;
	private float _disabledAlpha = 0.45f;

	private TextureButton? _textureButton;
	private RichTextLabel? _label;
	private MainMenu? _mainMenu;

	public override void _EnterTree()
	{
		// Needed so pause-menu buttons still work while GetTree().Paused is true.
		ProcessMode = ProcessModeEnum.Always;
	}

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always;

		CacheNodes();
		ConnectButtonSignals();
		ApplyEditorUpdate();
	}

	private void CacheNodes()
	{
		_textureButton = GetNodeOrNull<TextureButton>(_textureButtonPath);
		_label = GetNodeOrNull<RichTextLabel>(_labelPath);

		if (!Engine.IsEditorHint())
		{
			_mainMenu = FindParentMainMenu();

			if (_mainMenu == null)
			{
				GD.PrintErr($"{Name}: MainMenu could not be found in parent chain.");
			}
		}
	}

	private MainMenu? FindParentMainMenu()
	{
		Node? current = this;

		while (current != null)
		{
			if (current is MainMenu mainMenu)
			{
				return mainMenu;
			}

			current = current.GetParent();
		}

		return null;
	}

	private void ConnectButtonSignals()
	{
		if (_textureButton == null)
		{
			GD.PrintErr($"{Name}: TextureButton not found at path: {_textureButtonPath}");
			return;
		}

		Callable pressedCallable = Callable.From(OnPressed);
		Callable mouseEnteredCallable = Callable.From(OnMouseEntered);
		Callable mouseExitedCallable = Callable.From(OnMouseExited);

		if (!_textureButton.IsConnected(TextureButton.SignalName.Pressed, pressedCallable))
		{
			_textureButton.Pressed += OnPressed;
		}

		if (!_textureButton.IsConnected(Control.SignalName.MouseEntered, mouseEnteredCallable))
		{
			_textureButton.MouseEntered += OnMouseEntered;
		}

		if (!_textureButton.IsConnected(Control.SignalName.MouseExited, mouseExitedCallable))
		{
			_textureButton.MouseExited += OnMouseExited;
		}
	}

	private void ApplyEditorUpdate()
	{
		if (!IsInsideTree())
		{
			return;
		}

		CacheNodes();
		ApplyText();
		ApplyState();
	}

	private void ApplyText()
	{
		if (_label == null)
		{
			return;
		}

		_label.Text = _buttonText;
	}

	private void ApplyState()
	{
		bool isHidden = _state == MenuButtonState.Hidden;
		bool isDisabled = _state == MenuButtonState.Disabled;

		Visible = !isHidden;

		if (_textureButton != null)
		{
			_textureButton.Disabled = isDisabled || isHidden;
			_textureButton.ProcessMode = ProcessModeEnum.Always;
		}

		if (_label != null)
		{
			_label.ProcessMode = ProcessModeEnum.Always;
		}

		if (_useDisabledModulate && isDisabled)
		{
			Modulate = new Color(1.0f, 1.0f, 1.0f, _disabledAlpha);
		}
		else
		{
			Modulate = Colors.White;
		}

		Scale = Vector2.One;
	}

	private void OnPressed()
	{
		if (Engine.IsEditorHint())
		{
			return;
		}

		if (_state != MenuButtonState.Normal)
		{
			return;
		}

		MenuButtonAction? action = ActionResource as MenuButtonAction;

		if (action == null)
		{
			GD.PrintErr($"{Name}: ActionResource is not a valid MenuButtonAction.");
			return;
		}

		// Re-find it here too, because pause menu instances are created dynamically.
		if (_mainMenu == null || !IsInstanceValid(_mainMenu))
		{
			_mainMenu = FindParentMainMenu();
		}

		if (_mainMenu == null)
		{
			GD.PrintErr($"{Name}: MainMenu could not be found.");
			return;
		}

		action.Execute(this, _mainMenu);
	}

	private void OnMouseEntered()
	{
		if (_state != MenuButtonState.Normal)
		{
			return;
		}

		Scale = new Vector2(1.05f, 1.05f);
	}

	private void OnMouseExited()
	{
		Scale = Vector2.One;
	}

	public void SetNormal()
	{
		State = MenuButtonState.Normal;
	}

	public void SetDisabled()
	{
		State = MenuButtonState.Disabled;
	}

	public void SetHidden()
	{
		State = MenuButtonState.Hidden;
	}
}
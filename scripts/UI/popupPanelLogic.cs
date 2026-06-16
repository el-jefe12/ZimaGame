using Godot;
using System;

public enum PopupAction
{
	None = 0,
	NewGame = 1,
	ExitGame = 2,
	OverwriteSave = 3,
	HidePopup = 4
}

[Tool]
public partial class popupPanelLogic : Control // ✅ was Node
{
	public event Action<PopupAction> Confirmed;
	public event Action<PopupAction> Canceled;

	private RichTextLabel _label;
	private RichTextLabel _tooltip;
	private Button _goButton;
	private Button _cancelButton;

	private string _labelText = "";
	private string _tooltipText = "";
	private string _goButtonText = "Go";
	private string _cancelButtonText = "Cancel";
	private PopupAction _goaction = PopupAction.None;

	private PopupAction _cancelaction = PopupAction.HidePopup;

	private bool _wired = false; // ✅ prevents disconnect spam

	[Export]
	public string LabelText
	{
		get => _labelText;
		set { _labelText = value ?? ""; RequestRefresh(); }
	}

	[Export(PropertyHint.MultilineText)]
	public string TooltipText
	{
		get => _tooltipText;
		set { _tooltipText = value ?? ""; RequestRefresh(); }
	}

	[Export]
	public string GoButtonText
	{
		get => _goButtonText;
		set { _goButtonText = value ?? ""; RequestRefresh(); }
	}

	[Export]
	public string CancelButtonText
	{
		get => _cancelButtonText;
		set { _cancelButtonText = value ?? ""; RequestRefresh(); }
	}

	[Export]
	public PopupAction GoButtonAction
	{
		get => _goaction;
		set { _goaction = value; RequestRefresh(); }
	}

	[Export]
	public PopupAction CancelButtonAction
	{
		get => _cancelaction;
		set { _cancelaction = value; RequestRefresh(); }
	}

	public override void _Ready()
	{
		ResolveNodesAndApply();
	}

	public override void _EnterTree()
	{
		// Tool mode: safe refresh when entering tree
		CallDeferred(nameof(ResolveNodesAndApply));
	}

	private void RequestRefresh()
	{
		if (Engine.IsEditorHint())
			CallDeferred(nameof(ResolveNodesAndApply));
		else
			ApplyInspectorText();
	}

	private void ResolveNodesAndApply()
	{
		_label = GetNodeOrNull<RichTextLabel>("%PopupRichTextLabel");
		_tooltip = GetNodeOrNull<RichTextLabel>("%PopupTooltipRichTextLabel");
		_goButton = GetNodeOrNull<Button>("%GoButton");
		_cancelButton = GetNodeOrNull<Button>("%CancelButton");

		if (_label != null) _label.BbcodeEnabled = true;
		if (_tooltip != null) _tooltip.BbcodeEnabled = true;

		// ✅ Connect once; do NOT try to disconnect blindly
		if (!_wired)
		{
			if (_goButton != null) _goButton.Pressed += OnGoPressed;
			if (_cancelButton != null) _cancelButton.Pressed += OnCancelPressed;
			_wired = true;
		}

		ApplyInspectorText();
	}

	private void ApplyInspectorText()
	{
		if (_label != null) _label.Text = _labelText;
		if (_tooltip != null) _tooltip.Text = _tooltipText;
		if (_goButton != null) _goButton.Text = _goButtonText;
		if (_cancelButton != null) _cancelButton.Text = _cancelButtonText;
	}

	private void OnGoPressed() => Confirmed?.Invoke(_goaction);
	private void OnCancelPressed() => Canceled?.Invoke(_cancelaction);
}

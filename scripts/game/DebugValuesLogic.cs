using Godot;
using System;

public partial class DebugValuesLogic : Control
{
	[Export] public PlayerController Player;

	[Export] public raycast_viewLogic Player_raycast;

    private PlayerStats _stats;
    private PlayerNeedsSystem _needs;
    private PlayerHealthSystem _health;
    private PlayerTemperatureSystem _temperature;

	private PlayerFootsteps _footsteps;

	private RichTextLabel _FPSLabel;

	private TextureProgressBar _HungerTextureProgressBar;
	private TextureProgressBar _ThirstTextureProgressBar;
	private TextureProgressBar _TiredTextureProgressBar;

	private RichTextLabel _HungerValueRichTextLabel;
	private RichTextLabel _ThirstValueRichTextLabel;
	private RichTextLabel _TiredValueRichTextLabel;

	private RichTextLabel _MoveStateLabel;

	private RichTextLabel _GroundTypeLabel;

	private RichTextLabel _PPosLabel;

	private RichTextLabel _LookingAt;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_FPSLabel = GetNode<RichTextLabel>("%FPSLabel");

		_HungerTextureProgressBar = GetNode<TextureProgressBar>("%HungerTextureProgressBar");
		_ThirstTextureProgressBar = GetNode<TextureProgressBar>("%ThirstTextureProgressBar");
		_TiredTextureProgressBar = GetNode<TextureProgressBar>("%TiredTextureProgressBar");

		_HungerValueRichTextLabel = GetNode<RichTextLabel>("%HungerValueRichTextLabel");
		_ThirstValueRichTextLabel = GetNode<RichTextLabel>("%ThirstValueRichTextLabel");
		_TiredValueRichTextLabel = GetNode<RichTextLabel>("%TiredValueRichTextLabel");

		_MoveStateLabel = GetNode<RichTextLabel>("%MoveStateLabel");
		_GroundTypeLabel = GetNode<RichTextLabel>("%GroundTypeLabel");

		_PPosLabel = GetNode<RichTextLabel>("%PPosLabel");

		_LookingAt = GetNode<RichTextLabel>("%LookingAtLabel");


        _stats = Player.GetNodeOrNull<PlayerStats>("%PlayerStats");
        _needs = Player.GetNodeOrNull<PlayerNeedsSystem>("%PlayerNeeds");
        _health = Player.GetNodeOrNull<PlayerHealthSystem>("%PlayerHealth");
        _temperature = Player.GetNodeOrNull<PlayerTemperatureSystem>("%PlayerTemperature");
		_footsteps = Player.GetNodeOrNull<PlayerFootsteps>("%PlayerFootsteps");

        if (_stats == null)
            GD.PushError("playerHUD: PlayerStats node missing.");

        if (_needs == null)
            GD.PushError("playerHUD: PlayerNeedsSystem node missing.");

        if (_health == null)
            GD.PushError("playerHUD: PlayerHealthSystem node missing.");

        if (_temperature == null)
            GD.PushError("playerHUD: PlayerTemperatureSystem node missing.");


		if (Player == null)
		{
			GD.PushError("playerHUD: Player not assigned in inspector.");
			return;
		}

        _needs.HungerChanged += OnHungerChanged;
        _needs.ThirstChanged += OnThirstChanged;
        _needs.TiredChanged += OnTiredChanged;
		Player.MoveStateStatus += OnMoveStateStatusChanged;
		Player.PlayerPositionStatus += OnPlayerPositionChanged;
		Player.PlayerFootstepTexture += PlayerFootstepTexture;

		_footsteps.PlayerFootstep += PlayerFootstepTexture;

		Player_raycast.LookingAt += OnLookingAt;

		

		OnHungerChanged(_needs.Hunger, _needs.MaxHunger);
		OnThirstChanged(_needs.Thirst, _needs.MaxThirst);
		OnTiredChanged(_needs.Tired, _needs.MaxTired);

	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		_FPSLabel.Text = $"FPS: [color=Light_Green]{Engine.GetFramesPerSecond().ToString()}[/color]";
	}

	private void OnLookingAt(string nodeType, string nodeName, string nodePath)
	{

		_LookingAt.Text = $"LookingAt: [color=Light_Green]{nodePath}({nodeName})[/color]";

	}

	private void OnMoveStateStatusChanged(string movestate)
	{

		_MoveStateLabel.Text = $"MoveState: [color=Light_Green]{movestate}[/color]";

	}

	private void PlayerFootstepTexture(string texture_name)
	{

		_GroundTypeLabel.Text = $"GroundType: [color=Light_Green]{texture_name}[/color]";

	}

	private void OnPlayerPositionChanged(float x, float y, float z)
	{

		_PPosLabel.Text = $"Player Position: [color=red]X: {x}[/color] [color=green]Y: {y}[/color] [color=purple]Z: {z}[/color]";

	}

	private void OnHungerChanged(float current, float max)
	{
		_HungerTextureProgressBar.MaxValue = max;
		_HungerTextureProgressBar.Value = current;

		_HungerValueRichTextLabel.Text = current.ToString();
	}

	private void OnThirstChanged(float current, float max)
	{
		_ThirstTextureProgressBar.MaxValue = max;
		_ThirstTextureProgressBar.Value = current;

		_ThirstValueRichTextLabel.Text = current.ToString();

	}

	private void OnTiredChanged(float current, float max)
	{
		_TiredTextureProgressBar.MaxValue = max;
		_TiredTextureProgressBar.Value = current;

		_TiredValueRichTextLabel.Text = current.ToString();

	}
}

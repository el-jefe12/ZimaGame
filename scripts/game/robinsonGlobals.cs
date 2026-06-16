using Godot;

public partial class robinsonGlobals : Node
{
	public static robinsonGlobals Instance { get; private set; } = null!;

	// Global flags (instance-based)
	public bool ConsoleActive { get; set; } = false;
	public bool CanMove { get; set; } = true;
	public bool OpenInventory { get; set; } = false;

	public bool OpenItemUI { get; set; } = false;

	public bool FreeMouseCursor { get; set; } = false;

	public PackedScene? DeathScreenScene { get; private set; }

	private Node? _deathScreenInstance;

	public enum InventoryType
	{
		player_inventory = 0,
		player_inv_and_container = 1
	}

	// Current player reference
	public PlayerController? Player { get; private set; }

	public override void _EnterTree()
	{
		Instance = this;
	}

	public void RegisterPlayer(PlayerController p)
	{
		Player = p;
	}

	public void UnregisterPlayer(PlayerController p)
	{
		if (Player == p) Player = null;
	}

	// Time

	public override void _Ready()
	{
		DeathScreenScene = GD.Load<PackedScene>("res://scenes/death_screen.tscn");
	}

	public void ShowDeathScreen()
	{
		if (DeathScreenScene == null)
			return;

		if (_deathScreenInstance != null && IsInstanceValid(_deathScreenInstance))
			return;

		_deathScreenInstance = DeathScreenScene.Instantiate();
		GetTree().Root.AddChild(_deathScreenInstance);

		CanMove = false;
		ConsoleActive = false;
		Input.MouseMode = Input.MouseModeEnum.Visible;
	}

}

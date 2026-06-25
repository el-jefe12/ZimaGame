using Godot;
using System;

public partial class PlayerController : CharacterBody3D
{
	public enum MoveState
	{
		Idle = 0,
		Walk = 1,
		Sprint = 2,
		Jump = 3
	}

	[Export] public float MouseSensitivity = 0.1f;
	[Export] public float MovementSpeed = 6.0f;
	[Export] public float ExhaustedMovementSpeed = 3.0f;
	[Export] public float JumpVelocity = 4.5f;
	[Export] public float Gravity = 18.0f;
	[Export] public float MaxLookUpAngle = 89.0f;

	[Export] public float GroundAccel = 45.0f;
	[Export] public float GroundStopDecel = 60.0f;
	[Export] public float AirAccel = 10.0f;
	[Export] public float AirMaxSpeed = 8.0f;

	[Export] public float SprintSpeedMultiplier = 1.6f;


	[Signal] public delegate void MoveStateStatusEventHandler(string moveState);

	[Signal] public delegate void PlayerPositionStatusEventHandler(float x, float y, float z);

	[Signal] public delegate void PlayerFootstepTextureEventHandler(string texture_name);

	private CanvasLayer _PlayerUI;

	private PlayerHeadbob _headbob;
	private PlayerFallDamage _fallDamage;
	private PlayerHealthSystem _health;

	private PlayerFootsteps _playerFootsteps;

	private PlayerWaterHandler _waterHandler;

	private bool _wasOnFloor = false;
	private bool _fallTracking = false;
	private float _fallStartY = 0.0f;
	private float _highestYThisAir = 0.0f;

	private Node3D _yawPivot;
	private Node3D _pitchPivot;
	private Camera3D _camera;

	private float _pitchDeg = 0.0f;

	public MoveState CurrentMoveState = MoveState.Idle;

	private PlayerStats _stats;

	public override void _Ready()
	{

		if (robinsonGlobals.Instance != null)
		{
			robinsonGlobals.Instance.RegisterPlayer(this);

			// New player scene means the player should be movable again.
			robinsonGlobals.Instance.CanMove = true;
			robinsonGlobals.Instance.OpenInventory = false;
			robinsonGlobals.Instance.OpenItemUI = false;
			robinsonGlobals.Instance.FreeMouseCursor = false;
		}

		_PlayerUI = GetNodeOrNull<CanvasLayer>("%PlayerUICanvasLayer");
		_PlayerUI.Visible = true;
	
		_stats = GetNodeOrNull<PlayerStats>("%PlayerStats");

		if (_stats == null)
		{
			GD.PushError("PlayerController: PlayerStats node not found.");
			SetPhysicsProcess(false);
			return;
		}

		_yawPivot = GetNodeOrNull<Node3D>("%yawPivot");
		if (_yawPivot == null)
		{
			GD.PushError("PlayerController: yawPivot not found.");
			SetPhysicsProcess(false);
			return;
		}

		_pitchPivot = _yawPivot.GetNodeOrNull<Node3D>("%pitchPivot");
		if (_pitchPivot == null)
		{
			GD.PushError("PlayerController: pitchPivot not found.");
			SetPhysicsProcess(false);
			return;
		}

		_camera = GetNodeOrNull<Camera3D>("%CharacterCamera3D");
		if (_camera == null)
		{
			GD.PushError("PlayerController: CharacterCamera3D not found.");
			SetPhysicsProcess(false);
			return;
		}

		_waterHandler = GetNodeOrNull<PlayerWaterHandler>("%PlayerWaterHandler");

		if (_waterHandler == null)
		{
			GD.PushWarning("PlayerWaterHandler node missing. Water depth blocking disabled.");
		}

		_headbob = GetNodeOrNull<PlayerHeadbob>("%PlayerHeadbob");

		_fallDamage = GetNode<PlayerFallDamage>("%PlayerFallDamage");
		_health = GetNode<PlayerHealthSystem>("%PlayerHealth");

		_playerFootsteps = GetNodeOrNull<PlayerFootsteps>("%PlayerFootsteps");

		_health.PlayerDied += OnPlayerDied;

		if (_headbob == null)
		{
			GD.PushWarning("PlayerHeadbob node missing. Headbob disabled.");
		}

		_camera.Current = true;
		Input.MouseMode = Input.MouseModeEnum.Captured;

		// Initialize floor state correctly
		_wasOnFloor = IsOnFloor();
	}

	public override void _ExitTree()
	{
		if (robinsonGlobals.Instance != null)
			robinsonGlobals.Instance.UnregisterPlayer(this);
	}

	public override void _Input(InputEvent @event)
	{
		// Safety check. If the player is dead, do not allow pause menu,
		// inventory, mouse look, or other gameplay input.
		if (_health != null && _health.IsDead)
		{
			return;
		}

		if (@event.IsActionPressed("game_debug_ui_hide"))
		{
			if (_PlayerUI != null)
			{
				_PlayerUI.Visible = !_PlayerUI.Visible;
			}

			return;
		}

		if (@event.IsActionPressed("ui_cancel"))
		{
			if (PauseMenuManager.Instance != null)
			{
				PauseMenuManager.Instance.TogglePauseMenu();
			}

			GetViewport().SetInputAsHandled();
			return;
		}

		if (@event.IsActionPressed("game_open_inventory")
			&& IsOnFloor()
			&& robinsonGlobals.Instance != null
			&& !robinsonGlobals.Instance.ConsoleActive)
		{
			InventoryManager.Instance.TogglePlayerInventory();
			return;
		}

		if (Input.MouseMode != Input.MouseModeEnum.Captured)
		{
			return;
		}

		if (@event is InputEventMouseMotion motion)
		{
			float yawDeltaDeg = -motion.Relative.X * MouseSensitivity;
			_yawPivot.RotateY(Mathf.DegToRad(yawDeltaDeg));

			_pitchDeg -= motion.Relative.Y * MouseSensitivity;
			_pitchDeg = Mathf.Clamp(_pitchDeg, -MaxLookUpAngle, MaxLookUpAngle);

			_pitchPivot.RotationDegrees = new Vector3(_pitchDeg, 0f, 0f);
		}
	}

	private void OnPlayerDied()
	{
		Velocity = Vector3.Zero;

		Input.MouseMode = Input.MouseModeEnum.Visible;

		if (robinsonGlobals.Instance != null)
		{
			robinsonGlobals.Instance.CanMove = false;
			robinsonGlobals.Instance.ShowDeathScreen();
		}

		if (_PlayerUI != null)
		{
			_PlayerUI.Visible = false;
		}
	}


public override void _PhysicsProcess(double delta)
{
	float dt = (float)delta;

	if (_health != null && _health.IsDead)
		return;

	Vector3 v = Velocity;

	// =========================
	// GRAVITY
	// =========================

	if (!IsOnFloor())
		v.Y -= Gravity * dt;

	// =========================
	// INPUT
	// =========================

	Vector2 move2 = Input.GetVector(
		"game_move_left",
		"game_move_right",
		"game_move_backwards",
        "game_move_forward"
	);

	bool wantsMove = move2 != Vector2.Zero;
	bool sprintHeld = Input.IsActionPressed("game_move_run");

	bool canSprint = wantsMove && sprintHeld && IsOnFloor() && _stats.CanSprint();

	MoveState newState =
		!wantsMove ? MoveState.Idle :
		(canSprint ? MoveState.Sprint : MoveState.Walk);

	if (newState != CurrentMoveState)
	{
		CurrentMoveState = newState;
		EmitSignal(SignalName.MoveStateStatus, CurrentMoveState.ToString());
	}

	// =========================
	// MOVEMENT VECTOR
	// =========================

	Vector3 forward = -_yawPivot.GlobalTransform.Basis.Z;
	Vector3 right = _yawPivot.GlobalTransform.Basis.X;

	Vector3 wishDir = Vector3.Zero;

	if (wantsMove && _stats.CanMove())
		wishDir = (right * move2.X + forward * move2.Y).Normalized();

	float baseSpeed = _stats.IsSlowWalk()
		? ExhaustedMovementSpeed
		: MovementSpeed;

	float wishSpeed = baseSpeed;

	if (IsOnFloor() && canSprint)
		wishSpeed *= SprintSpeedMultiplier;

	Vector3 horizVel = new Vector3(v.X, 0.0f, v.Z);

	if (IsOnFloor())
	{
		if (wishDir != Vector3.Zero)
		{
			Vector3 target = wishDir * wishSpeed;
			horizVel = horizVel.MoveToward(target, GroundAccel * dt);
		}
		else
		{
			horizVel = horizVel.MoveToward(Vector3.Zero, GroundStopDecel * dt);
		}
	}
	else
	{
		if (wishDir != Vector3.Zero)
		{
			Vector3 airTarget = wishDir * Mathf.Min(wishSpeed, AirMaxSpeed);
			horizVel = horizVel.MoveToward(airTarget, AirAccel * dt);
		}
	}

	v.X = horizVel.X;
	v.Z = horizVel.Z;

	// =========================
	// WATER
	// =========================

	if (_waterHandler != null)
	{
		Vector3 predictedPosition = GlobalPosition + new Vector3(v.X, 0.0f, v.Z) * dt;

		if (_waterHandler.WouldBeTooDeepAtPosition(predictedPosition))
		{
			v.X = 0.0f;
			v.Z = 0.0f;
		}
		else
		{
			v.X *= _waterHandler.MovementMultiplier;
			v.Z *= _waterHandler.MovementMultiplier;
		}
	}

	// =========================
	// JUMP
	// =========================

	if (IsOnFloor() && Input.IsActionJustPressed("game_move_jump"))
	{
		if (_stats.TryJump())
		{
			v.Y = JumpVelocity;

			CurrentMoveState = MoveState.Jump;
			EmitSignal(SignalName.MoveStateStatus, CurrentMoveState.ToString());
		}
	}

	// =========================
	// APPLY MOVEMENT
	// =========================

	Velocity = v;
	MoveAndSlide();

	// =========================
	// FALL DAMAGE (AFTER MOVE)
	// =========================

	bool onFloorNow = IsOnFloor();

	// Start tracking as soon as the player is airborne (even if they started in the air).
	if (!onFloorNow && !_fallTracking)
	{
		_fallTracking = true;
		_fallStartY = GlobalPosition.Y;
		_highestYThisAir = _fallStartY;
	}

	if (_fallTracking && !onFloorNow)
	{
		float y = GlobalPosition.Y;

		if (y > _highestYThisAir)
			_highestYThisAir = y;
	}

	if (onFloorNow && _fallTracking)
	{
		if (_fallDamage != null && _health != null)
		{
			float fallDistance = _highestYThisAir - GlobalPosition.Y;

			GD.Print($"[FallCheck] distance = {fallDistance}");

			float dmg = _fallDamage.CalculateDamage(fallDistance);

			if (dmg > 0)
				_health.Damage(dmg);
		}

		_fallTracking = false;
	}

	_wasOnFloor = onFloorNow;

	// =========================
	// FOOTSTEPS
	// =========================

	_playerFootsteps.Tick(
		GlobalPosition,
		wantsMove,
		_stats.CanMove(),
		IsOnFloor(),
		canSprint
	);

	// =========================
	// POSITION SIGNAL
	// =========================

	Vector3 pos = GlobalPosition;
	EmitSignal(SignalName.PlayerPositionStatus, pos.X, pos.Y, pos.Z);

	// =========================
	// HEADBOB
	// =========================

	if (_headbob != null)
	{
		float stamina01 = _stats.Stamina / _stats.MaxStamina;

		float maxSpeedForSpeed01 = canSprint
			? baseSpeed * SprintSpeedMultiplier
			: baseSpeed;

		_headbob.Tick(
			dt,
			Velocity,
			IsOnFloor(),
			wantsMove,
			canSprint,
			_stats.CanMove(),
			!_stats.CanMove(),
			stamina01,
			maxSpeedForSpeed01
		);
	}

	// =========================
	// STAMINA
	// =========================

	_stats.TickStamina(dt, wantsMove, canSprint);
}
}

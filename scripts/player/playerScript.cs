using System;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.SqlTypes;
using Godot;

/// <summary>
/// FPS controller + stamina system:
/// - Sprint (Shift) drains stamina per second
/// - Walking drains stamina per second (small)
/// - Jump costs a chunk of stamina
/// - Regen starts only after a delay since last stamina use
/// - If stamina hits 0 -> player is forced to stop moving until it regens above 0
/// </summary>
public partial class playerScript : CharacterBody3D
{

    public enum MoveState
    {
        Idle = 0,
        Walk = 1,
        Sprint = 2,
        Jump = 3
    }

    private PlayerHeadbob _headbob = null!;

    private CanvasLayer _PlayerUI;

    public MoveState _moveState = MoveState.Idle;

    private Node? _deathScreenInstance;

    // =========================
    // Player State
    // =========================
    [ExportCategory("Player State")]    
    [Export] public bool isDead = false;

    // =========================
    // Push / Physics Interaction
    // =========================
    [ExportCategory("Push / Physics Interaction")]
    [Export] public bool EnablePushing = true;

    // Constant push force (Newtons). Same force => heavy stuff moves less (mass matters).
    [Export] public float PushForce = 70.0f;

    [Export] public float MaxPushForce = 120.0f;

    // Limit how hard you can push based on your current speed (helps prevent "explosions")
    [Export] public float PushForceSpeedFactor = 10.0f; // extra N per m/s of player speed

    // Keep pushes mostly horizontal (prevents lifting / popping)
    [Export] public bool PushHorizontalOnly = true;

    // Optional: don't push absurdly heavy objects at all
    [Export] public float MaxPushMass = 250.0f;

    // Optional: cooldown so we don't apply force multiple times per frame to same body
    private readonly Godot.Collections.Dictionary<ulong, double> _pushCooldownUntil = new();
    [Export] public float PushPerBodyCooldownSeconds = 0.0f; // set to 0 to disable

    [ExportCategory("Push / Physics Interaction")]
    [Export] public float MaxPushedBodySpeed = 10.0f;       // overall cap
    [Export] public float MaxPushedBodyDownSpeed = 6.0f;    // downward cap

    [ExportCategory("Fall Damage - Height")]
    [Export] public bool UseHeightFallDamage = true;
    [Export] public float SafeFallDistance = 3.0f;      // meters with no damage
    [Export] public float LethalFallDistance = 18.0f;   // meters that would be lethal
    [Export] public float MaxFallDamage = 100.0f;

    private bool _wasOnFloor = false;
    private bool _fallTracking = false;
    private float _fallStartY = 0.0f;
    private float _highestYThisAir = 0.0f;

    // =========================
    // Footsteps (Terrain3D)
    // =========================
    private FootstepManager? _footstepManager = null;

    [ExportCategory("Footsteps")]
    [Export] public float StepDistanceWalk = 1.65f;

    [Export] public float StepDistanceSprint = 1.15f;

    // Horizontal-distance accumulator (meters)
    private float _stepDistanceAccum = 0.0f;

    // =========================
    // Tunables (Inspector)
    // =========================
    
    [ExportCategory("Tunables")]
    [Export] public float MouseSensitivity = 0.1f;
    [Export] public float MovementSpeed = 6.0f;
    [Export] public float ExhaustedMovementSpeed = 3.0f;
    [Export] public float JumpVelocity = 4.5f;
    [Export] public float Gravity = 18.0f;
    [Export] public float MaxLookUpAngle = 89.0f;
    [Export] public float GroundDecel = 20.0f;

    [ExportCategory("Tunables - Movement")]
    [Export] public float GroundAccel = 45.0f;      // how fast you reach target speed on ground
    [Export] public float GroundStopDecel = 60.0f;  // how fast you stop when no input on ground
    [Export] public float AirAccel = 10.0f;         // how much steering you get in air
    [Export] public float AirMaxSpeed = 8.0f;       // optional cap for mid-air strafe speed (set <= sprint speed)


    // =========================
    // Stamina (Inspector)
    // =========================
    [ExportCategory("Stamina")]
    [Export] public float MaxStamina = 100.0f;
    [Export] public float Stamina = 100.0f;

    [ExportCategory("Stamina Cap Setters")] // This is supposed to decide when a cap for stamina is supposed to apply. The cap 
    [Export] public float StaminaCapHungryLow = 0.5f;
    [Export] public float StaminaCapHungryMedium = 0.35f;
    [Export] public float StaminaCapHungryStarving = 0.2f;
    [Export] public float StaminaCapThirstyLow = 0.75f;
    [Export] public float StaminaCapThirstyMedium = 0.5f;
    [Export] public float StaminaCapThirstyParched = 0.35f;

    [Export] public float StaminaCapTiredLow = 0.75f;
    [Export] public float StaminaCapTiredMedium = 0.5f;
    [Export] public float StaminaCapTiredExhausted = 0.35f;

    // Sprinting
    [Export] public float SprintSpeedMultiplier = 1.6f;
    [Export] public float SprintDrainPerSecond = 10.0f;

    // Walking
    [Export] public float WalkDrainPerSecond = 0.5f;

    // Jump stamina cost (chunk)
    [Export] public float JumpStaminaCost = 15.0f;

    // Regen behavior
    [Export] public float StaminaRegenPerSecond = 14.0f;
    [Export] public float RegenDelaySeconds = 2.5f;

    // If stamina is basically 0, we treat it as 0 and lock movement
    [Export] public float ExhaustedThreshold = 0.01f;

    // If stamina reaches specified low amount, it cancels + makes it impossible to run, and only allows walking
    [Export] public float ExhaustedCancelSprintTreshold = 30f;

    [Export] public float ExhaustedSlowWalkTreshold = 15f;

    // =========================
    // Tired (Inspector)
    // =========================

    [ExportCategory("Tiredness")]

    [Export] public float MaxTired = 100.0f;
    [Export] public float Tired = 100.0f;

    [Export] public float TiredDecayPerMinute = 0.08f;

    [Export] public float TiredDecreasePerMinute = 0.10f; // Naming is all wonky but this means how much gets added back when Idle (resting will be handled separately)

    [Export] public float TiredMultiplierWalk = 2.0f;
    [Export] public float TiredMultiplierSprint = 5.0f;

    [Export] public float TiredStaticJump = 1.5f;

    // Values for when the exhaustion effects should spawn
    [ExportCategory("Tiredness Effect Triggers")]
    [Export] public float TiredValueTriggerForEffectLow = 75.0f;
    [Export] public float TiredValueTriggerForEffectMedium = 45.0f;
    [Export] public float TiredValueTriggerForEffectHigh = 25.0f;

    // =========================
    // Health (Inspector)
    // =========================
    [ExportCategory("Health")]
    [Export] public float MaxHealth = 100.0f;
    [Export] public float Health = 100.0f;
    [ExportCategory("Health Decay When Meters Are Empty")]
    [Export] public float HealthMultiplierDecayWhenHungerEmpty = 1.2f;
    [Export] public float HealthMultiplierDecayWhenTiredEmpty = 1.2f;
    [Export] public float HealthMultiplierDecayWhenThirstEmpty = 1.2f;

    [ExportCategory("Health Decay When Meters Are Empty")]
    [Export] public float HealthDamagePerMinutePerPenaltyUnit = 0.25f; // tweak this

    [ExportCategory("Temperature")]
    [Export] public float MaxTemperature = 100.0f;
    [Export] public float MinTemperature = -100.0f;
    [Export] public float PlayerTemperature = 36.4f; // should be in celsius, with ranges from -100 to +100 (both should have resulted in death way before they are able to be hit)

    [Export] public float PlayerTemperatureJacketWarmth; // Decides how warm Player's equipment is, since there is no clothing in the game. Esentially should provide some warmth, which will 

    [Export] public float PlayerTemperatureLowest = 34.2f; // this is how low the temperature should be able to go. Can be changed by different factors, such as hunger, exhaustion,

    [Export] public float PlayerTemperatureDecayPerMinute = 0.15f; //Decay should be 
    [Export] public float PlayerTemperatureMultiplierWalk = 0.25f;
    [Export] public float PlayerTemperatureMultiplierSprint = 0.37f;    

    // =========================
    // Hunger and Thirst (Inspector)
    // =========================
    [ExportCategory("Hunger")]
      [Export] public float MaxHunger = 100.0f;
      [Export] public float CurrentHunger = 100.0f;
      [Export] public float HungerDecayPerMinute = 0.15f;
    
      [Export] public float HungerMultiplierWalk = 1.9f;
      [Export] public float HungerMultiplierSprint = 5.5f;

      [Export] public float HungerStaticJump = 0.25f;

    [ExportCategory("Hunger Effect Triggers")]
      [Export] public float HungerValueTriggerForEffectLow = 75.0f;
      [Export] public float HungerValueTriggerForEffectMedium = 45.0f;
      [Export] public float HungerValueTriggerForEffectHigh = 25.0f;

    [ExportCategory("Thirst")]
      [Export] public float MaxThirst = 100.0f;
      [Export] public float CurrentThirst = 100.0f;
      [Export] public float ThirstDecayPerMinute = 0.15f;  

      [Export] public float ThirstMultiplierWalk = 2.8f;
      [Export] public float ThirstMultiplierSprint = 12.0f;

      [Export] public float ThirstStaticJump = 0.25f;


    [ExportCategory("Thirst Effect Triggers")]
      [Export] public float ThirstValueTriggerForEffectLow = 75.0f;
      [Export] public float ThirstValueTriggerForEffectMedium = 45.0f;
      [Export] public float ThirstValueTriggerForEffectHigh = 25.0f;

    // =========================
    // UI signals (HUD listens to these)
    // =========================
    [Signal] public delegate void HealthChangedEventHandler(float current, float max);
    [Signal] public delegate void StaminaChangedEventHandler(float current, float max);

    [Signal] public delegate void HungerChangedEventHandler(float current, float max);
    [Signal] public delegate void ThirstChangedEventHandler(float current, float max);
    [Signal] public delegate void TiredChangedEventHandler(float current, float max);

    [Signal] public delegate void PlayerTemperatureChangedEventHandler(float minTemp, float MaxTemp, float currentPlayerTemp);

    [Signal] public delegate void InventoryStatusEventHandler(bool status, robinsonGlobals.InventoryType type);

    [Signal] public delegate void MoveStateStatusEventHandler(string moveState);

    [Signal] public delegate void PlayerPositionStatusEventHandler(float x, float y, float z);

    [Signal] public delegate void PlayerFootstepTextureEventHandler(string texture_name);

    private float _lastHealth = -99999.0f;
    private float _lastStamina = -99999.0f;

    private float _lastHunger = -99999.0f;
    private float _lastThirst = -99999.0f;
    private float _lastTired = -99999.0f;

    private float _lastPlayerTemperature = -99999.0f;

    private Vector3 _playerPosition;


    // =========================
    // Node refs
    // =========================

    private Node3D _yawPivot = null!;
    private Node3D _pitchPivot = null!;
    private Camera3D _camera = null!;
    private float _pitchDeg = 0.0f;

    // Regen delay timer (counts down to 0)
    private float _regenCooldown = 0.0f;

    public override void _Ready()
    {
        if (robinsonGlobals.Instance != null)
            //robinsonGlobals.Instance.RegisterPlayer(this);

        if (WorldTime.Instance != null)
            WorldTime.Instance.MinuteTick += OnMinuteTick;

        _yawPivot = GetNodeOrNull<Node3D>("%yawPivot");
        if (_yawPivot == null)
        {
            GD.PushError("Missing child node 'yawPivot' under the Player.");
            SetProcess(false);
            SetPhysicsProcess(false);
            return;
        }

        _pitchPivot = _yawPivot.GetNodeOrNull<Node3D>("%pitchPivot");
        if (_pitchPivot == null)
        {
            GD.PushError("Missing node 'yawPivot/pitchPivot'.");
            SetProcess(false);
            SetPhysicsProcess(false);
            return;
        }
        
        _headbob = GetNodeOrNull<PlayerHeadbob>("PlayerHeadbob");
        if (_headbob == null)
        {
            GD.PushWarning("PlayerHeadbob node missing. Headbob disabled.");
        }

        _camera = GetNodeOrNull<Camera3D>("%CharacterCamera3D");
        if (_camera == null)
        {
            GD.PushError("Missing node 'yawPivot/pitchPivot/cameraBob/CharacterCamera3D'.");
            SetProcess(false);
            SetPhysicsProcess(false);
            return;
        }

        _camera.Current = true;
        Input.MouseMode = Input.MouseModeEnum.Captured;

        _PlayerUI = GetNodeOrNull<CanvasLayer>("%PlayerUICanvasLayer");
        _PlayerUI.Visible = true;

        // --- FootstepManager singleton reference ---
        _footstepManager = GetNodeOrNull<FootstepManager>("/root/FootstepManager");

        if (_footstepManager == null)
        {
            GD.PushWarning("[playerScript] FootstepManager AutoLoad not found at /root/FootstepManager");
        }
        else
        {
            GD.Print($"[playerScript] FootstepManager found. Terrain registered = {_footstepManager.IsTerrainRegistered()}");
        }


        // Clamp + emit once so UI initializes correctly
        SetHealth(Health, emitNow: true);
        SetStamina(Stamina, emitNow: true);

        SetHunger(CurrentHunger, emitNow: true);
        SetThirst(CurrentThirst, emitNow: true);
        SetTired(Tired, emitNow: true);

        SetPlayerTemperature(PlayerTemperature, emitNow: true);

        GetPlayerPosition(true);

        EmitFootstepDebug();

        GD.Print("playerScript ready.");
    }

    public override void _ExitTree()
    {
        if (robinsonGlobals.Instance != null)
            //robinsonGlobals.Instance.UnregisterPlayer(this);

        if (WorldTime.Instance != null)
            WorldTime.Instance.MinuteTick -= OnMinuteTick;

    }

    public override void _Input(InputEvent @event)
    {

        if (@event.IsActionPressed("game_debug_ui_hide"))
        {
            if (_PlayerUI.Visible)
            {
                _PlayerUI.Visible = false;
            }
            else
            {
                _PlayerUI.Visible = true;
            }
        }

        if (@event.IsActionPressed("ui_cancel"))
            Input.MouseMode = Input.MouseModeEnum.Visible;

        if (isDead)
            return;

        if (@event.IsActionPressed("game_open_inventory")
            && !isDead
            && IsOnFloor()
            && robinsonGlobals.Instance != null
            && !robinsonGlobals.Instance.ConsoleActive)
        {
            InventoryManager.Instance.TogglePlayerInventory();
        }


        if (Input.MouseMode != Input.MouseModeEnum.Captured)
            return;

        if (@event is InputEventMouseMotion motion)
        {
            float yawDeltaDeg = -motion.Relative.X * MouseSensitivity;
            _yawPivot.RotateY(Mathf.DegToRad(yawDeltaDeg));

            _pitchDeg -= motion.Relative.Y * MouseSensitivity;
            _pitchDeg = Mathf.Clamp(_pitchDeg, -MaxLookUpAngle, MaxLookUpAngle);
            _pitchPivot.RotationDegrees = new Vector3(_pitchDeg, 0.0f, 0.0f);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        GetPlayerPosition(true);

        if (isDead)
        {
            // Ensure you don't drift if something sets velocity elsewhere
            Velocity = Vector3.Zero;
            MoveAndSlide();
            return;
        }

        bool onFloorNow = IsOnFloor();

        // Detect leaving ground (floor -> air)
        if (!onFloorNow && _wasOnFloor)
        {
            _fallTracking = true;
            _fallStartY = GlobalPosition.Y;
            _highestYThisAir = _fallStartY; // in case you jump upward first
        }

        // Track highest point while airborne (so jumps don't inflate fall distance)
        if (_fallTracking && !onFloorNow)
        {
            float y = GlobalPosition.Y;
            if (y > _highestYThisAir)
                _highestYThisAir = y;
        }

        // Detect landing (air -> floor)
        if (onFloorNow && !_wasOnFloor)
        {
            if (_fallTracking && UseHeightFallDamage && !isDead)
            {
                float landY = GlobalPosition.Y;

                // fall distance should be from highest point reached to landing point
                float fallDistance = _highestYThisAir - landY;

                ApplyFallDamageFromDistance(fallDistance);
            }

            _fallTracking = false;
        }

        _wasOnFloor = onFloorNow;


        float dt = (float)delta;

        // =========================
        // Stamina state helpers
        // =========================

        bool hardExhausted = Stamina <= ExhaustedThreshold;
        bool slowWalk = Stamina <= ExhaustedSlowWalkTreshold;
        bool sprintLockedOut = Stamina <= ExhaustedCancelSprintTreshold;

        // Movement input vector
        Vector2 move2 = Input.GetVector("game_move_left", "game_move_right", "game_move_backwards", "game_move_forward");
        bool wantsMove = move2 != Vector2.Zero;

        bool sprintHeld = Input.IsActionPressed("game_move_run");

        bool canSprint = wantsMove && sprintHeld && IsOnFloor() && !hardExhausted && !sprintLockedOut;

        if (!wantsMove || !robinsonGlobals.Instance.CanMove) {
            _moveState = MoveState.Idle;
            //GD.Print($"[MoveState]: {_moveState}");
            EmitSignal(SignalName.MoveStateStatus, _moveState.ToString());
        }
        else {
            _moveState = canSprint ? MoveState.Sprint : MoveState.Walk;
            //GD.Print($"[MoveState]: {_moveState}");
            EmitSignal(SignalName.MoveStateStatus, _moveState.ToString());
        }

        // Any stamina usage should reset regen delay
        bool staminaUsedThisFrame = false;

        // =========================
        // Gravity
        // =========================
        Vector3 v = Velocity;

        if (!IsOnFloor())
            v.Y -= Gravity * dt;

        // =========================
        // Jump (costs chunk)
        // =========================
        if (IsOnFloor() && Input.IsActionJustPressed("game_move_jump"))
        {
            if ( robinsonGlobals.Instance.CanMove && Stamina >= JumpStaminaCost)
            {
                v.Y = JumpVelocity;
                _regenCooldown = RegenDelaySeconds;
                staminaUsedThisFrame = true;

                _moveState = MoveState.Jump;
                //GD.Print($"[MoveState]: {_moveState}");

                Stamina = ApplyJumpNeedsCost(Stamina, JumpStaminaCost, MaxStamina);
                SetStamina(Stamina, true);

                Tired = ApplyJumpNeedsCost(Tired, TiredStaticJump, MaxTired);
                SetTired(Tired, true);

                EmitSignal(SignalName.MoveStateStatus, _moveState.ToString());
            }
        }

        // Recompute stamina states after jump
        hardExhausted = Stamina <= ExhaustedThreshold;
        slowWalk = Stamina <= ExhaustedSlowWalkTreshold;
        sprintLockedOut = Stamina <= ExhaustedCancelSprintTreshold;

        // =========================
        // Drain stamina for movement (walk OR sprint)
        // =========================
        if (robinsonGlobals.Instance.CanMove && wantsMove && !hardExhausted)
        {
            float drainPerSecond = canSprint ? SprintDrainPerSecond : WalkDrainPerSecond;
            float drain = drainPerSecond * dt;

            Stamina -= drain;
            Stamina = Mathf.Clamp(Stamina, 0.0f, MaxStamina);

            _regenCooldown = RegenDelaySeconds;
            staminaUsedThisFrame = true;
        }

        // Recompute stamina states after movement drain
        hardExhausted = Stamina <= ExhaustedThreshold;
        slowWalk = Stamina <= ExhaustedSlowWalkTreshold;
        sprintLockedOut = Stamina <= ExhaustedCancelSprintTreshold;

        // =========================
        // Stamina regen (after delay)
        // =========================
        if (!staminaUsedThisFrame)
        {
            if (_regenCooldown > 0.0f)
            {
                _regenCooldown -= dt;
                if (_regenCooldown < 0.0f)
                    _regenCooldown = 0.0f;
            }
            else
            {
                if (Stamina < MaxStamina)
                {
                    Stamina += StaminaRegenPerSecond * dt;
                    Stamina = Mathf.Clamp(Stamina, 0.0f, MaxStamina);
    
                }
            }
        }

        // Recompute stamina states after regen
        hardExhausted = Stamina <= ExhaustedThreshold;
        slowWalk = Stamina <= ExhaustedSlowWalkTreshold;
        sprintLockedOut = Stamina <= ExhaustedCancelSprintTreshold;

        // =========================
        // Movement application (accel/decel + keep jump momentum)
        // =========================
        bool onFloor = IsOnFloor();

        Vector3 forward = -_yawPivot.GlobalTransform.Basis.Z;
        Vector3 right = _yawPivot.GlobalTransform.Basis.X;

        // Desired move dir from input
        Vector3 wishDir = Vector3.Zero;
        if (robinsonGlobals.Instance.CanMove && wantsMove && !hardExhausted)
            wishDir = (right * move2.X + forward * move2.Y).Normalized();

        // Match your speed logic
        float baseSpeed = (slowWalk ? ExhaustedMovementSpeed : MovementSpeed);
        float wishSpeed = baseSpeed;

        // Only allow sprint speed choice while grounded (keeps “run jump momentum” stable)
        if (onFloor && canSprint)
            wishSpeed *= SprintSpeedMultiplier;

        // Current horizontal velocity
        Vector3 horizVel = new Vector3(v.X, 0.0f, v.Z);

        if (onFloor)
        {
            if (wishDir != Vector3.Zero)
            {
                Vector3 target = wishDir * wishSpeed;
                horizVel = horizVel.MoveToward(target, GroundAccel * dt);
            }
            else
            {
                // Stop quicker -> less slidy
                horizVel = horizVel.MoveToward(Vector3.Zero, GroundStopDecel * dt);
            }
        }
        else
        {
            // Air control: steer toward desired direction without killing existing momentum
            if (wishDir != Vector3.Zero)
            {
                Vector3 airTarget = wishDir * Mathf.Min(wishSpeed, AirMaxSpeed);
                horizVel = horizVel.MoveToward(airTarget, AirAccel * dt);
            }
            // else: keep existing horizVel (pure momentum)
        }

        v.X = horizVel.X;
        v.Z = horizVel.Z;

        Velocity = v;
        MoveAndSlide();

        if (EnablePushing)
            ApplyPushForces(dt);

        // =========================
        // Footstep trigger (distance-based)
        // =========================
        if (_footstepManager != null && _footstepManager.IsTerrainRegistered())
        {
            // Only count footsteps when actually moving on the ground
            if (IsOnFloor() && robinsonGlobals.Instance.CanMove && wantsMove && !hardExhausted)
            {
                // Use horizontal speed (ignore vertical)
                horizVel = new Vector3(Velocity.X, 0.0f, Velocity.Z);
                float horizSpeed = horizVel.Length();

                if (horizSpeed > 0.1f)
                {
                    _stepDistanceAccum += horizSpeed * dt;

                    float stepDist = canSprint ? StepDistanceSprint : StepDistanceWalk;

                    if (_stepDistanceAccum >= stepDist)
                    {
                        _stepDistanceAccum = 0.0f;
                        EmitFootstepDebug();
                    }
                }
            }
            else
            {
                // Optional: reset so steps don't "store up" while stopping
                _stepDistanceAccum = 0.0f;
            }
        }

        bool canMove = (robinsonGlobals.Instance != null) && robinsonGlobals.Instance.CanMove;

        float stamina01 = (MaxStamina > 0.001f) ? Mathf.Clamp(Stamina / MaxStamina, 0.0f, 1.0f) : 1.0f;

        // This matches your movement speed logic closely:
        baseSpeed = (Stamina <= ExhaustedSlowWalkTreshold) ? ExhaustedMovementSpeed : MovementSpeed;
        float maxSpeedForSpeed01 = canSprint ? baseSpeed * SprintSpeedMultiplier : baseSpeed;

        _headbob.Tick(
            dt,
            Velocity,
            IsOnFloor(),
            wantsMove,
            canSprint,
            canMove,
            hardExhausted,
            stamina01,
            maxSpeedForSpeed01
        );

        _NotifyHudIfChanged();
    }


    private void ApplyPushForces(float dt)
    {
        int count = GetSlideCollisionCount();
        if (count == 0) return;

        Vector3 horizVel = new Vector3(Velocity.X, 0f, Velocity.Z);
        float speed = horizVel.Length();

        float forceMag = PushForce + (speed * PushForceSpeedFactor);
        forceMag = Mathf.Min(forceMag, MaxPushForce);

        for (int i = 0; i < count; i++)
        {
            var col = GetSlideCollision(i);
            if (col.GetCollider() is not RigidBody3D rb)
                continue;

            if (rb.Freeze) continue;
            if (rb.Mass > MaxPushMass) continue;

            Vector3 dir = -col.GetNormal();
            if (PushHorizontalOnly) dir.Y = 0f;
            if (dir.LengthSquared() < 0.0001f) continue;
            dir = dir.Normalized();

            float into = Mathf.Max(0f, horizVel.Dot(dir));
            if (into <= 0.05f) continue;

            Vector3 contactPos = col.GetPosition();
            Vector3 offset = contactPos - rb.GlobalPosition;

            float scaled = forceMag * Mathf.Clamp(into, 0.0f, 1.0f);
            rb.ApplyForce(dir * scaled, offset);

            // Safety clamps to prevent tunneling through floor
            Vector3 lv = rb.LinearVelocity;

            if (lv.Y < -MaxPushedBodyDownSpeed)
                lv.Y = -MaxPushedBodyDownSpeed;

            float len = lv.Length();
            if (len > MaxPushedBodySpeed)
                lv = lv / len * MaxPushedBodySpeed;

            rb.LinearVelocity = lv;
        }
    }

    // =========================
    // Small helper API (safe setters)
    // =========================

    public void SetHealth(float value, bool emitNow = true)
    {
        Health = Mathf.Clamp(value, 0.0f, MaxHealth);

        if (emitNow)
        {
            _lastHealth = Health;
            EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
        }

        // Death trigger (after clamp)
        if (!isDead && Health <= 0.0f)
            Die();

    }

    public void SetStamina(float value, bool emitNow = true)
    {
        Stamina = Mathf.Clamp(value, 0.0f, MaxStamina);

        if (emitNow)
        {
            _lastStamina = Stamina;
            EmitSignal(SignalName.StaminaChanged, Stamina, MaxStamina);
        }
    }

    public void SetHunger(float value, bool emitNow = true)
    {
        CurrentHunger = Mathf.Clamp(value, 0.0f, MaxHunger);

        if (emitNow)
        {
            _lastHunger = CurrentHunger;
            EmitSignal(SignalName.HungerChanged, CurrentHunger, MaxHunger);
        }
    }

    public void SetThirst(float value, bool emitNow = true)
    {
        CurrentThirst = Mathf.Clamp(value, 0.0f, MaxThirst);

        if (emitNow)
        {
            _lastThirst = CurrentThirst;
            EmitSignal(SignalName.ThirstChanged, CurrentThirst, MaxThirst);
        }
    }

    public void SetTired(float value, bool emitNow = true)
    {
        Tired = Mathf.Clamp(value, 0.0f, MaxTired);

        if (emitNow)
        {
            _lastTired = Tired;
            EmitSignal(SignalName.TiredChanged, Tired, MaxTired);
        }
    }

    public void SetPlayerTemperature(float value, bool emitNow = true)
    {
        PlayerTemperature = Mathf.Clamp(value, MinTemperature, MaxTemperature);

        if (emitNow)
        {
            _lastPlayerTemperature = PlayerTemperature;
            EmitSignal(
                SignalName.PlayerTemperatureChanged,
                MinTemperature,
                MaxTemperature,
                PlayerTemperature
            );
        }
    }

    public void GetPlayerPosition(bool emitNow = true)
    {
        Vector3 pos = GlobalPosition;

        if (emitNow)
        {
            _playerPosition = pos;
            EmitSignal(
                SignalName.PlayerPositionStatus,
                pos.X,
                pos.Y,
                pos.Z
            );
        }
    }

    public void GetPlayerFootstepTexture(string texture_name, bool emitNow = true)
    {
        if (emitNow)
        {
            EmitSignal(
                SignalName.PlayerFootstepTexture,
                texture_name
            );
        }
    }

    private void OnMinuteTick(int day, int hour, int minute) // hunger thirst tired health changes
    {

        if (isDead)
            return;

        float healthMult = 1.0f;
        float staminaMult = 1.0f;

        float hungerMult = 1.0f;
        float thirstMult = 1.0f;
        float TiredMult = 1.0f;

        if (_moveState == MoveState.Walk)
        {
            hungerMult = HungerMultiplierWalk;
            thirstMult = ThirstMultiplierWalk;
            TiredMult = TiredMultiplierWalk;

            GD.Print($"Based on MoveState [{_moveState}], applying Decay for Hunger: [{hungerMult}], for Tired: [{TiredMult}]for Thirst: [{thirstMult}].");
        }
        else if (_moveState == MoveState.Sprint)
        {
            hungerMult = HungerMultiplierSprint;
            thirstMult = ThirstMultiplierSprint;
            TiredMult = TiredMultiplierSprint;

            GD.Print($"Based on MoveState [{_moveState}], applying Decay for Hunger: [{hungerMult}], for Tired: [{TiredMult}]for Thirst: [{thirstMult}].");
        }

        CurrentHunger = Mathf.Max(0, CurrentHunger - (HungerDecayPerMinute * hungerMult));
        CurrentThirst = Mathf.Max(0, CurrentThirst - (ThirstDecayPerMinute * thirstMult));
        Tired = Mathf.Max(0, Tired - (TiredDecayPerMinute * TiredMult));

        SetHunger(CurrentHunger, true);
        SetThirst(CurrentThirst, true);
        SetTired(Tired, true);

        ApplyHealthDecayFromNeedsEmpty_Additive();
    }

    private void ApplyHealthDecayFromNeedsEmpty_Additive()
    {
        float penalty = 0.0f;

        if (CurrentHunger <= 0.0f) penalty += HealthMultiplierDecayWhenHungerEmpty; // e.g. 1.2
        if (CurrentThirst <= 0.0f) penalty += HealthMultiplierDecayWhenThirstEmpty; // e.g. 1.2
        if (Tired <= 0.0f) penalty += HealthMultiplierDecayWhenTiredEmpty; // e.g. 1.2

        // If nothing is empty -> no health decay at all
        if (penalty <= 0.0f)
            return;

        float damage = HealthDamagePerMinutePerPenaltyUnit * penalty;

        Health = Mathf.Max(0.0f, Health - damage);
        SetHealth(Health, true);
    }

    private void SetStaminaCapBasedOnStats()
    {
        
    }

    private void ApplyAllJumpNeedsCost()
    {

        Stamina -= JumpStaminaCost; // removes a chunk from stamina
        Stamina = Mathf.Clamp(Stamina, 0.0f, MaxStamina);

        CurrentHunger = Mathf.Clamp(CurrentHunger - HungerStaticJump, 0.0f, MaxHunger); // adjusts hunger based on 
        CurrentThirst = Mathf.Clamp(CurrentThirst - ThirstStaticJump, 0.0f, MaxThirst);
        Tired = Mathf.Clamp(Tired - TiredStaticJump, 0.0f, MaxTired);

        SetHunger(CurrentHunger, true);
        SetThirst(CurrentThirst, true);
        SetTired(Tired, true);
    }

    private float ApplyJumpNeedsCost(float currentValue, float valueDecrease, float maxValue)
    {
        currentValue -= valueDecrease;
        return Mathf.Clamp(currentValue, 0.0f, maxValue);
    }

    private void _NotifyHudIfChanged()
    {
        if (!Mathf.IsEqualApprox(Health, _lastHealth))
        {
            _lastHealth = Health;
            EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
        }

        if (!Mathf.IsEqualApprox(Stamina, _lastStamina))
        {
            _lastStamina = Stamina;
            EmitSignal(SignalName.StaminaChanged, Stamina, MaxStamina);
        }

        if (!Mathf.IsEqualApprox(PlayerTemperature, _lastPlayerTemperature))
        {
            _lastPlayerTemperature = PlayerTemperature;
            EmitSignal(SignalName.PlayerTemperatureChanged, MinTemperature, MaxTemperature, PlayerTemperature);
        }
    }

    private void EmitFootstepDebug()
    {
        if (_footstepManager == null)
            return;

        // Feet position: for FPS this is usually fine.
        // If you want slightly below, use: GlobalPosition + Vector3.Down * 0.1f
        Vector3 feetPos = GlobalPosition;

        if (_footstepManager.TryGetDominantTexture(feetPos, out int id, out string name))
        {
            //GD.Print($"[Footstep] id={id} name='{name}' pos={feetPos}");
            GetPlayerFootstepTexture(name, true);
        }
        else
        {
            GD.Print("[Footstep] No Terrain3D surface detected (outside terrain or hole).");
        }
    }

    private void ApplyFallDamageFromDistance(float fallDistance)
    {
        if (fallDistance <= SafeFallDistance)
            return;

        float t = (fallDistance - SafeFallDistance) / Mathf.Max(0.001f, (LethalFallDistance - SafeFallDistance));
        t = Mathf.Clamp(t, 0.0f, 1.0f);

        float dmg = (t * t) * MaxFallDamage; // quadratic ramp
        GD.Print($"[FallDamage] distance={fallDistance:0.00}m -> damage={dmg:0.0}");

        SetHealth(Health - dmg, true);
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;

        // Stop any stamina regen cooldown logic etc. if you want
        _regenCooldown = 0.0f;

        // Freeze player motion
        Velocity = Vector3.Zero;

        // Release mouse (optional: depends on your UI flow)
        Input.MouseMode = Input.MouseModeEnum.Visible;

        // Hard disable gameplay input via your global gate (recommended)
        if (robinsonGlobals.Instance != null)
        {
            robinsonGlobals.Instance.CanMove = false;
            // Add these if you have them, or equivalent:
            // robinsonGlobals.Instance.CanInteract = false;
            // robinsonGlobals.Instance.CanOpenUI = false;
            // robinsonGlobals.Instance.ConsoleActive = false;
        }

        // Unsubscribe from world ticking so needs/health won't keep changing after death
        if (WorldTime.Instance != null)
            WorldTime.Instance.MinuteTick -= OnMinuteTick;

        _PlayerUI.Visible = false;

        if (robinsonGlobals.Instance != null)
            robinsonGlobals.Instance.ShowDeathScreen();
    }

    public void Respawn(Vector3 spawnPos)
    {
        isDead = false;

        Health = MaxHealth;
        Stamina = MaxStamina;
        CurrentHunger = MaxHunger;
        CurrentThirst = MaxThirst;
        Tired = MaxTired;

        GlobalPosition = spawnPos;
        Velocity = Vector3.Zero;

        if (WorldTime.Instance != null)
            WorldTime.Instance.MinuteTick += OnMinuteTick;

        if (robinsonGlobals.Instance != null)
            robinsonGlobals.Instance.CanMove = true;

        if (_deathScreenInstance != null && IsInstanceValid(_deathScreenInstance))
            _deathScreenInstance.QueueFree();
        _deathScreenInstance = null;

        Input.MouseMode = Input.MouseModeEnum.Captured;

        // Emit HUD refresh
        SetHealth(Health, true);
        SetStamina(Stamina, true);
        SetHunger(CurrentHunger, true);
        SetThirst(CurrentThirst, true);
        SetTired(Tired, true);
        SetPlayerTemperature(PlayerTemperature, true);
    }

}

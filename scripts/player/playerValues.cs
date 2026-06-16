using Godot;

/// <summary>
/// Holds ALL player meters and their rules:
/// - Stamina drain/regen
/// - Hunger/Thirst/Tired minute tick
/// - Health decay when needs are empty
/// - Temperature values (just storage for now)
///
/// This node does NOT move the character.
/// </summary>
public partial class PlayerValues : Node
{

    public enum MoveState
    {
        Idle = 0,
        Walk = 1,
        Sprint = 2,
        Jump = 3
    }

    public MoveState _moveState = MoveState.Idle;

    // =========================
    // Signals (HUD listens to these)
    // =========================
    [Signal] public delegate void HealthChangedEventHandler(float current, float max);
    [Signal] public delegate void StaminaChangedEventHandler(float current, float max);
    [Signal] public delegate void HungerChangedEventHandler(float current, float max);
    [Signal] public delegate void ThirstChangedEventHandler(float current, float max);
    [Signal] public delegate void TiredChangedEventHandler(float current, float max);
    [Signal] public delegate void PlayerTemperatureChangedEventHandler(float minTemp, float maxTemp, float currentPlayerTemp);


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

		// Regen delay timer (counts down to 0)
		private float _regenCooldown = 0.0f;

		// Last-values for spam-free signals
		private float _lastHealth = -99999.0f;
		private float _lastStamina = -99999.0f;
		private float _lastHunger = -99999.0f;
		private float _lastThirst = -99999.0f;
		private float _lastTired = -99999.0f;
		private float _lastPlayerTemperature = -99999.0f;

    public override void _Ready()
    {
        // Emit initial values so HUD initializes correctly
        SetHealth(Health, true);
        SetStamina(Stamina, true);
        SetHunger(CurrentHunger, true);
        SetThirst(CurrentThirst, true);
        SetTired(Tired, true);
        SetPlayerTemperature(PlayerTemperature, true);
    }

    // ============================================================
    // Public API for playerScript
    // ============================================================

    public float GetStamina01()
    {
        if (MaxStamina <= 0.001f) return 1.0f;
        return Mathf.Clamp(Stamina / MaxStamina, 0.0f, 1.0f);
    }

    public bool IsHardExhausted() => Stamina <= ExhaustedThreshold;
    public bool IsSprintLockedOut() => Stamina <= ExhaustedCancelSprintTreshold;
    public bool IsSlowWalk() => Stamina <= ExhaustedSlowWalkTreshold;

    public void NotifyStaminaUsed()
    {
        _regenCooldown = RegenDelaySeconds;
    }

    public bool TryConsumeJumpCosts()
    {
        if (Stamina < JumpStaminaCost)
            return false;

        Stamina = ApplyNeedsCost(Stamina, JumpStaminaCost, MaxStamina);
        SetStamina(Stamina, true);

        Tired = ApplyNeedsCost(Tired, TiredStaticJump, MaxTired);
        SetTired(Tired, true);

        // If you want hunger/thirst jump hits too, enable:
        // CurrentHunger = ApplyNeedsCost(CurrentHunger, HungerStaticJump, MaxHunger);
        // CurrentThirst = ApplyNeedsCost(CurrentThirst, ThirstStaticJump, MaxThirst);
        // SetHunger(CurrentHunger, true);
        // SetThirst(CurrentThirst, true);

        NotifyStaminaUsed();
        return true;
    }

    public void TickStaminaPhysics(float dt, bool wantsMove, bool canSprint, bool canMove)
    {
        bool staminaUsedThisFrame = false;

        bool hardExhausted = IsHardExhausted();

        // Drain for movement
        if (canMove && wantsMove && !hardExhausted)
        {
            float drainPerSecond = canSprint ? SprintDrainPerSecond : WalkDrainPerSecond;
            float drain = drainPerSecond * dt;

            Stamina -= drain;
            Stamina = Mathf.Clamp(Stamina, 0.0f, MaxStamina);

            _regenCooldown = RegenDelaySeconds;
            staminaUsedThisFrame = true;
        }

        // Regen (only if not used)
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

        // Emit if changed
        _NotifyIfChanged();
    }

    public void TickMinute(playerScript.MoveState moveState)
    {
        float hungerMult = 1.0f;
        float thirstMult = 1.0f;
        float tiredMult = 1.0f;

        if (moveState == playerScript.MoveState.Walk)
        {
            hungerMult = HungerMultiplierWalk;
            thirstMult = ThirstMultiplierWalk;
            tiredMult = TiredMultiplierWalk;
        }
        else if (moveState == playerScript.MoveState.Sprint)
        {
            hungerMult = HungerMultiplierSprint;
            thirstMult = ThirstMultiplierSprint;
            tiredMult = TiredMultiplierSprint;
        }

        CurrentHunger = Mathf.Max(0.0f, CurrentHunger - (HungerDecayPerMinute * hungerMult));
        CurrentThirst = Mathf.Max(0.0f, CurrentThirst - (ThirstDecayPerMinute * thirstMult));
        Tired = Mathf.Max(0.0f, Tired - (TiredDecayPerMinute * tiredMult));

        SetHunger(CurrentHunger, true);
        SetThirst(CurrentThirst, true);
        SetTired(Tired, true);

        ApplyHealthDecayFromNeedsEmpty_Additive();
    }

    // ============================================================
    // Setters (emit safe)
    // ============================================================

    public void SetHealth(float value, bool emitNow = true)
    {
        Health = Mathf.Clamp(value, 0.0f, MaxHealth);
        if (emitNow)
        {
            _lastHealth = Health;
            EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
        }
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
            EmitSignal(SignalName.PlayerTemperatureChanged, MinTemperature, MaxTemperature, PlayerTemperature);
        }
    }

    // ============================================================
    // Internals
    // ============================================================

    private void ApplyHealthDecayFromNeedsEmpty_Additive()
    {
        float penalty = 0.0f;

        if (CurrentHunger <= 0.0f) penalty += HealthMultiplierDecayWhenHungerEmpty;
        if (CurrentThirst <= 0.0f) penalty += HealthMultiplierDecayWhenThirstEmpty;
        if (Tired <= 0.0f) penalty += HealthMultiplierDecayWhenTiredEmpty;

        if (penalty <= 0.0f)
            return;

        float damage = HealthDamagePerMinutePerPenaltyUnit * penalty;
        Health = Mathf.Max(0.0f, Health - damage);
        SetHealth(Health, true);
    }

    private static float ApplyNeedsCost(float currentValue, float valueDecrease, float maxValue)
    {
        currentValue -= valueDecrease;
        return Mathf.Clamp(currentValue, 0.0f, maxValue);
    }

    private void _NotifyIfChanged()
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

        if (!Mathf.IsEqualApprox(CurrentHunger, _lastHunger))
        {
            _lastHunger = CurrentHunger;
            EmitSignal(SignalName.HungerChanged, CurrentHunger, MaxHunger);
        }

        if (!Mathf.IsEqualApprox(CurrentThirst, _lastThirst))
        {
            _lastThirst = CurrentThirst;
            EmitSignal(SignalName.ThirstChanged, CurrentThirst, MaxThirst);
        }

        if (!Mathf.IsEqualApprox(Tired, _lastTired))
        {
            _lastTired = Tired;
            EmitSignal(SignalName.TiredChanged, Tired, MaxTired);
        }

        if (!Mathf.IsEqualApprox(PlayerTemperature, _lastPlayerTemperature))
        {
            _lastPlayerTemperature = PlayerTemperature;
            EmitSignal(SignalName.PlayerTemperatureChanged, MinTemperature, MaxTemperature, PlayerTemperature);
        }
    }
}

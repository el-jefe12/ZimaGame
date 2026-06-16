using Godot;
using System;

public partial class PlayerNeedsSystem : Node
{
    [Signal] public delegate void HungerChangedEventHandler(float current, float max);
    [Signal] public delegate void ThirstChangedEventHandler(float current, float max);
    [Signal] public delegate void TiredChangedEventHandler(float current, float max);

    [Export] public float MaxHunger = 100.0f;
    [Export] public float Hunger = 100.0f;
    [Export] public float HungerDecayPerMinute = 0.15f;

    [Export] public float MaxThirst = 100.0f;
    [Export] public float Thirst = 100.0f;
    [Export] public float ThirstDecayPerMinute = 0.15f;

    [Export] public float MaxTired = 100.0f;
    [Export] public float Tired = 100.0f;
    [Export] public float TiredDecayPerMinute = 0.08f;

    [Export] public float HungerMultiplierWalk = 1.9f;
    [Export] public float HungerMultiplierSprint = 5.5f;

    [Export] public float ThirstMultiplierWalk = 2.8f;
    [Export] public float ThirstMultiplierSprint = 12.0f;

    [Export] public float TiredMultiplierWalk = 2.0f;
    [Export] public float TiredMultiplierSprint = 5.0f;

    [Export] public float WorldTimeReconnectIntervalSeconds = 1.0f;

    private bool _connectedToWorldTime = false;
    private float _reconnectTimer = 0.0f;

    private float _lastEmittedHunger = -99999.0f;
    private float _lastEmittedThirst = -99999.0f;
    private float _lastEmittedTired = -99999.0f;

    public override void _Ready()
    {
        GD.Print("[PlayerNeedsSystem] _Ready");

        SetHunger(Hunger, true);
        SetThirst(Thirst, true);
        SetTired(Tired, true);

        TryConnectWorldTime();
        SetProcess(!_connectedToWorldTime);
    }

    public override void _Process(double delta)
    {
        if (_connectedToWorldTime)
        {
            SetProcess(false);
            return;
        }

        _reconnectTimer -= (float)delta;

        if (_reconnectTimer > 0.0f)
            return;

        _reconnectTimer = WorldTimeReconnectIntervalSeconds;

        GD.Print("[PlayerNeedsSystem] Retrying WorldTime connection...");
        TryConnectWorldTime();

        if (_connectedToWorldTime)
            SetProcess(false);
    }

    public override void _ExitTree()
    {
        DisconnectWorldTime();
    }

    private void TryConnectWorldTime()
    {
        if (_connectedToWorldTime)
            return;

        if (WorldTime.Instance == null)
        {
            GD.PrintErr("[PlayerNeedsSystem] TryConnectWorldTime failed: WorldTime.Instance is null.");
            return;
        }

        // Safety: remove first in case of duplicate init/reload
        WorldTime.Instance.MinuteTick -= OnMinuteTick;
        WorldTime.Instance.MinuteTick += OnMinuteTick;

        _connectedToWorldTime = true;

        GD.Print("[PlayerNeedsSystem] Connected to WorldTime.MinuteTick successfully.");
    }

    private void DisconnectWorldTime()
    {
        if (!_connectedToWorldTime)
            return;

        if (WorldTime.Instance != null)
            WorldTime.Instance.MinuteTick -= OnMinuteTick;

        _connectedToWorldTime = false;

        GD.Print("[PlayerNeedsSystem] Disconnected from WorldTime.MinuteTick.");
    }

    public void SetHunger(float value, bool emitNow = true)
    {
        Hunger = Mathf.Clamp(value, 0.0f, MaxHunger);

        if (!emitNow)
            return;

        if (!Mathf.IsEqualApprox(Hunger, _lastEmittedHunger))
        {
            _lastEmittedHunger = Hunger;
            EmitSignal(SignalName.HungerChanged, Hunger, MaxHunger);
        }
    }

    public void SetThirst(float value, bool emitNow = true)
    {
        Thirst = Mathf.Clamp(value, 0.0f, MaxThirst);

        if (!emitNow)
            return;

        if (!Mathf.IsEqualApprox(Thirst, _lastEmittedThirst))
        {
            _lastEmittedThirst = Thirst;
            EmitSignal(SignalName.ThirstChanged, Thirst, MaxThirst);
        }
    }

    public void SetTired(float value, bool emitNow = true)
    {
        Tired = Mathf.Clamp(value, 0.0f, MaxTired);

        if (!emitNow)
            return;

        if (!Mathf.IsEqualApprox(Tired, _lastEmittedTired))
        {
            _lastEmittedTired = Tired;
            EmitSignal(SignalName.TiredChanged, Tired, MaxTired);
        }
    }

    private void OnMinuteTick(int day, int hour, int minute)
    {
        GD.Print($"[PlayerNeedsSystem] OnMinuteTick fired: Day={day} Hour={hour} Minute={minute}");

        PlayerController player = null;

        if (robinsonGlobals.Instance != null)
            player = robinsonGlobals.Instance.Player;

        float hungerMult = 1.0f;
        float thirstMult = 1.0f;
        float tiredMult = 1.0f;

        if (player != null)
        {
            if (player.CurrentMoveState == PlayerController.MoveState.Walk)
            {
                hungerMult = HungerMultiplierWalk;
                thirstMult = ThirstMultiplierWalk;
                tiredMult = TiredMultiplierWalk;
            }
            else if (player.CurrentMoveState == PlayerController.MoveState.Sprint)
            {
                hungerMult = HungerMultiplierSprint;
                thirstMult = ThirstMultiplierSprint;
                tiredMult = TiredMultiplierSprint;
            }

            GD.Print(
                $"[PlayerNeedsSystem] MoveState={player.CurrentMoveState} | " +
                $"hungerMult={hungerMult} thirstMult={thirstMult} tiredMult={tiredMult}"
            );
        }
        else
        {
            GD.Print("[PlayerNeedsSystem] Player not available yet, applying idle decay.");
        }

        float newHunger = Hunger - (HungerDecayPerMinute * hungerMult);
        float newThirst = Thirst - (ThirstDecayPerMinute * thirstMult);
        float newTired = Tired - (TiredDecayPerMinute * tiredMult);

        SetHunger(newHunger, true);
        SetThirst(newThirst, true);
        SetTired(newTired, true);

        GD.Print(
            $"[PlayerNeedsSystem] New values | " +
            $"Hunger={Hunger:0.00} Thirst={Thirst:0.00} Tired={Tired:0.00}"
        );
    }
}
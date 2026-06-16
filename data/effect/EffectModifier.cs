using Godot;

[GlobalClass]
public partial class EffectModifier : Resource
{
    public enum ModifierTarget
    {
        Health = 0,
        Stamina = 1,
        MaxHealth = 2,
        MaxStamina = 3,
        MoveSpeed = 4,
        HungerDrain = 5,
        ThirstDrain = 6,
        TiredDrain = 7,
        BodyTemperature = 8,
        ColdResistance = 9,
        HeatResistance = 10,
        BleedingRate = 11,
        CarryWeight = 12,
    }

    public enum ModifierOperation
    {
        Add = 0,
        Multiply = 1,
        CapMultiplier = 2,
        Set = 3,
    }

    public enum ModifierTiming
    {
        Passive = 0,
        OverTime = 1,
        OnApply = 2,
        OnRemove = 3,
    }

    [Export] public ModifierTarget Target { get; set; } = ModifierTarget.Health;
    [Export] public ModifierOperation Operation { get; set; } = ModifierOperation.Add;
    [Export] public ModifierTiming Timing { get; set; } = ModifierTiming.Passive;

    [Export] public float Value { get; set; } = 0.0f;

    [Export] public float TickInterval { get; set; } = 1.0f;

    [Export] public int Priority { get; set; } = 0;
}
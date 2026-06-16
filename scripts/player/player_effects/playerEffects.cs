using Godot;
using System;
using System.Collections.Generic;

public abstract class EffectStat
{
    // If you want an identifier, make it string or enum, not int.
    public string Id { get; init; } = "";
}

public sealed class HealthStat : EffectStat
{
    public double MaxAdd { get; init; } = 0.0;
    public double RegenAdd { get; init; } = 0.0;
}

public sealed class StaminaStat : EffectStat
{
    public double MaxAdd { get; init; } = 0.0;
    public double RegenAdd { get; init; } = 0.0;
}

public sealed class PlayerEffect
{
    public string Name { get; init; } = "";
    public Texture2D Icon { get; init; }

    public List<EffectStat> Stats { get; } = new List<EffectStat>();
}


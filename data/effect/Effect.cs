using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class Effect : Resource
{
    public enum Severity
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Deadly = 3
    }

    public enum EType
    {
        Hunger = 0,
        Thirst = 1,
        Tired = 2,
        Temperature = 3,
        Bleeding = 4,
    }

    [Export] public Severity EffectSeverity { get; set; }

    [Export] public string EffectName = "";
    [Export] public Texture2D Icon;

    [Export(PropertyHint.MultilineText)]
    public string EffectDescription = "";

    [Export] public bool IsBuff = false;

    // Example: hunger, thirst, tired, cold, bleeding, infection
    [Export] public EType Type;

    [Export] public int Priority { get; set; } = 0;

    // This is the important part.
    // Add as many modifier resources as you want in the inspector.
    [Export] public Godot.Collections.Array<EffectModifier> Modifiers { get; set; } = new();
}
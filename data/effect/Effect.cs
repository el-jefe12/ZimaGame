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
		Infection = 5,
		Poison = 6,
		Custom = 7
	}

	public enum StackMode
	{
		// Applying the same effect again resets its duration.
		RefreshDuration = 0,

		// Applying the same effect again creates another separate copy.
		StackSeparate = 1,

		// Applying the same effect again does nothing.
		IgnoreIfAlreadyActive = 2
	}

	[ExportCategory("Identity")]
	[Export] public string EffectId { get; set; } = "";
	[Export] public string EffectName = "";
	[Export] public Texture2D? Icon;

	[Export(PropertyHint.MultilineText)]
	public string EffectDescription = "";

	[Export] public bool IsBuff = false;

	// Example: hunger, thirst, tired, cold, bleeding, infection.
	[Export] public EType Type;

	[Export] public Severity EffectSeverity { get; set; }

	[Export] public int Priority { get; set; } = 0;

	[ExportCategory("Duration")]
	[Export] public bool IsPermanent { get; set; } = false;
	[Export] public float DurationSeconds { get; set; } = 10.0f;

	[ExportCategory("Stacking")]
	[Export] public StackMode EffectStackMode { get; set; } = StackMode.RefreshDuration;

	[ExportCategory("Modifiers")]
	[Export] public Godot.Collections.Array<EffectModifier> Modifiers { get; set; } = new();
}
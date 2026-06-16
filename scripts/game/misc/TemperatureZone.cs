using Godot;
using System;

[GlobalClass]
public partial class TemperatureZone : Area3D
{
    // Higher = wins when overlapping
    [Export] public int Priority { get; set; } = 0;

    // If true, this zone sets a specific environment temperature (e.g., indoors)
    [Export] public bool OverridesTemperature { get; set; } = true;

    // Used when OverridesTemperature = true
    [Export] public float TemperatureC { get; set; } = 18.0f;

    // Extra multipliers you can use for “this area is windy / sheltered”
    [Export] public float HeatLossMultiplier { get; set; } = 1.0f; // 0.5 sheltered, 2.0 blizzard

    // Optional: “additive warmth” sources (like standing near a fire)
    [Export] public float WarmthBonusC { get; set; } = 0.0f; // +10 near a campfire, etc.
}

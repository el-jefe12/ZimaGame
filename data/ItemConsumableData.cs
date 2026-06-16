using Godot;

[GlobalClass]
public partial class ItemConsumableData : Resource
{
    [Export] public float HealthRestore = 0;
    [Export] public float HungerRestore = 0;
    [Export] public float ThirstRestore = 0;
    [Export] public float TiredRestore = 0;
}

using Godot;
using System;

public sealed class PaintedInstance
{
    public MultiMeshInstance3D Instancer;
    public int InstanceIndex;
    public Vector3 GlobalPosition;
	public PackedScene GameplayScene;
}
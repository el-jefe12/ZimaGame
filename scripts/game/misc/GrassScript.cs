using Godot;
using System;

public partial class GrassScript : Node3D
{

	private Area3D _grassArea3D;

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
		_grassArea3D = GetNodeOrNull<Area3D>("grassArea3D");
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}

	public void _on_grass_area_3d_body_entered(Node3D body)
	{
		if (body.IsInGroup("player"))
		{
			GD.Print($"{body.Name} entered Grass");
		}
	}	
	public void _on_grass_area_3d_body_exited(Node3D body)
	{
		if (body.IsInGroup("player"))
		{
			GD.Print($"{body.Name} exited Grass");				
		}	
	}
}

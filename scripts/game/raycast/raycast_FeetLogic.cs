using Godot;
using System;

public partial class raycast_FeetLogic : RayCast3D
{
	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _PhysicsProcess(double delta)
    {
        if (!IsColliding())
            return;

        GodotObject colliderObj = GetCollider();

        if (colliderObj is Node node)
        {
            string nodeType = node.GetType().Name;   // C# class type
            string nodeName = node.Name;             // Node name in scene
            string nodePath = node.GetPath();        // Optional: full scene path

            GD.Print($"[RaycastFeet] Type: {nodeType} | Name: {nodeName} | Path: {nodePath}");
        }
        else
        {
            GD.Print($"[RaycastFeet] Collider is not a Node. Type: {colliderObj.GetType().Name}");
        }
    }
}

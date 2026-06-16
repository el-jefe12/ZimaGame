using Godot;
using System;

public partial class raycast_viewLogic : RayCast3D
{
    [Export(PropertyHint.None, "Prints out info about what is being looked at into the console.")]
    public bool verbal = false;
    [Signal] public delegate void LookingAtEventHandler(string nodeType, string nodeName, string nodePath);

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
            
            EmitSignal(SignalName.LookingAt, nodeType, nodeName, nodePath);
            if (verbal)
                GD.Print($"Type: {nodeType} | Name: {nodeName} | Path: {nodePath}");
        }
        else
        {
            GD.Print($"Collider is not a Node. Type: {colliderObj.GetType().Name}");
        }
    }
}

using Godot;
using System;

public partial class PlayerPushSystem : Node
{
    [Export] public float PushForce = 70.0f;
    [Export] public float MaxPushForce = 120.0f;
    [Export] public float PushForceSpeedFactor = 10.0f;

    public void ApplyPush(CharacterBody3D player)
    {
        int count = player.GetSlideCollisionCount();

        for (int i = 0; i < count; i++)
        {
            var col = player.GetSlideCollision(i);

            if (col.GetCollider() is not RigidBody3D rb)
                continue;

            Vector3 dir = -col.GetNormal();
            dir.Y = 0;

            rb.ApplyForce(dir * PushForce, col.GetPosition() - rb.GlobalPosition);
        }
    }
}
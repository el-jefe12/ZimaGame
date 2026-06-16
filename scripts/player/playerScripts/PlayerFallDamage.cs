using Godot;
using System;

public partial class PlayerFallDamage : Node
{
    [Export] public float SafeFallDistance = 3.0f;
    [Export] public float LethalFallDistance = 18.0f;
    [Export] public float MaxFallDamage = 100.0f;

    public float CalculateDamage(float fallDistance)
    {
        if (fallDistance <= SafeFallDistance)
            return 0;

        float t = (fallDistance - SafeFallDistance) /
                  Mathf.Max(0.001f, (LethalFallDistance - SafeFallDistance));

        t = Mathf.Clamp(t, 0, 1);

        return (t * t) * MaxFallDamage;
    }
}
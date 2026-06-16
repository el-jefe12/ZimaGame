using Godot;
using System;

public partial class PlayerFootsteps : Node
{
    [Signal] public delegate void PlayerFootstepEventHandler(string textureName);

    [Export] public float StepDistanceWalk = 1.65f;
    [Export] public float StepDistanceSprint = 1.15f;

    private FootstepManager _footstepManager;

    private float _stepDistanceAccum = 0.0f;
    private Vector3 _lastPosition;
    private bool _initialized = false;

    public override void _Ready()
    {
        _footstepManager = GetNodeOrNull<FootstepManager>("/root/FootstepManager");

        if (_footstepManager == null)
            GD.PushError("PlayerFootsteps: FootstepManager AutoLoad not found.");
    }

    /// <summary>
    /// Called every frame by PlayerController.
    /// Handles step distance accumulation internally.
    /// </summary>
    public void Tick(Vector3 playerPosition, bool isMoving, bool canMove, bool isGrounded, bool sprinting)
    {
        if (!_initialized)
        {
            _lastPosition = playerPosition;
            _initialized = true;
            return;
        }

        if (!isGrounded || !isMoving || !canMove)
        {
            _stepDistanceAccum = 0.0f;
            _lastPosition = playerPosition;
            return;
        }

        float distance = playerPosition.DistanceTo(_lastPosition);
        _stepDistanceAccum += distance;

        float stepDistance = sprinting ? StepDistanceSprint : StepDistanceWalk;

        if (_stepDistanceAccum >= stepDistance)
        {
            _stepDistanceAccum = 0.0f;
            EmitFootstep(playerPosition);
        }

        _lastPosition = playerPosition;
    }

    private void EmitFootstep(Vector3 feetPosition)
    {
        if (_footstepManager == null)
            return;

        if (_footstepManager.TryGetDominantTexture(feetPosition, out int id, out string name))
        {
            EmitSignal(SignalName.PlayerFootstep, name);
        }
        else
        {
            GD.Print("[Footstep] No Terrain3D surface detected.");
        }
    }
}
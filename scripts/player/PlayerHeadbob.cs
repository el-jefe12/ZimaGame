using Godot;
using System;

public partial class PlayerHeadbob : Node
{
    [ExportCategory("Refs")]
    [Export] public NodePath CameraBobPath { get; set; } = new NodePath(); // e.g. "../yawPivot/pitchPivot/cameraBob"

    [ExportCategory("Breathing (idle)")]
    [Export] public float BreathPosAmplitude = 0.015f;
    [Export] public float BreathFrequency = 0.25f;
    [Export] public float BreathTiltAmplitudeDeg = 0.35f;

    [ExportCategory("Movement bob")]
    [Export] public float WalkBobPosAmplitude = 0.04f;
    [Export] public float SprintBobPosAmplitude = 0.06f;
    [Export] public float WalkBobFrequency = 1.7f;
    [Export] public float SprintBobFrequency = 2.4f;
    [Export] public float BobTiltAmplitudeDeg = 1.1f;

    [ExportCategory("Smoothing")]
    [Export] public float BobBlendSpeed = 12.0f;

    [ExportCategory("Exhaustion scaling")]
    [Export] public float ExhaustBreathMultiplier = 2.0f;
    [Export] public float ExhaustBobMultiplier = 1.25f;

    private Node3D? _cameraBob = null;
    private Vector3 _basePos = Vector3.Zero;
    private Vector3 _baseRot = Vector3.Zero;
    private float _t = 0.0f;

    public override void _Ready()
    {
        _cameraBob = GetNodeOrNull<Node3D>(CameraBobPath);
        if (_cameraBob == null)
        {
            GD.PushError("PlayerHeadbob: CameraBobPath invalid. Set it to your cameraBob Node3D.");
            SetProcess(false);
            return;
        }

        _basePos = _cameraBob.Position;
        _baseRot = _cameraBob.Rotation;
    }

    /// <summary>
    /// Call once per frame (ideally from player _PhysicsProcess after MoveAndSlide()).
    /// Inputs are already computed by the player controller.
    /// </summary>
    public void Tick(
        float dt,
        Vector3 velocity,
        bool isGrounded,
        bool wantsMove,
        bool canSprint,
        bool canMove,
        bool hardExhausted,
        float stamina01,
        float maxSpeedForSpeed01
    )
    {   
        if (_cameraBob == null)
            return;

        _t += dt;

        float exhausted01 = 1.0f - Mathf.Clamp(stamina01, 0.0f, 1.0f);

        // Horizontal speed
        float horizSpeed = new Vector2(velocity.X, velocity.Z).Length();

        // Normalized movement strength (0..1)
        float speed01 = (maxSpeedForSpeed01 > 0.001f)
            ? Mathf.Clamp(horizSpeed / maxSpeedForSpeed01, 0.0f, 1.0f)
            : 0.0f;

        // ---------
        // Breathing
        // ---------
        float breathMoveFade = 1.0f - (speed01 * 0.35f); // slight reduction while moving
        float breathMul = Mathf.Lerp(1.0f, ExhaustBreathMultiplier, exhausted01) * breathMoveFade;

        float breathY = Mathf.Sin(_t * Mathf.Tau * BreathFrequency) * BreathPosAmplitude * breathMul;
        float breathTiltZ = Mathf.Sin(_t * Mathf.Tau * BreathFrequency) * Mathf.DegToRad(BreathTiltAmplitudeDeg) * breathMul;

        Vector3 breathPos = new Vector3(0.0f, breathY, 0.0f);
        Vector3 breathRot = new Vector3(0.0f, 0.0f, breathTiltZ);

        // ------------
        // Movement bob
        // ------------
        bool doMoveBob =
            isGrounded &&
            canMove &&
            wantsMove &&
            !hardExhausted &&
            horizSpeed > 0.1f;

        float moveY = 0.0f;
        float moveTilt = 0.0f;

        if (doMoveBob)
        {
            float bobAmp = canSprint ? SprintBobPosAmplitude : WalkBobPosAmplitude;
            float bobFreq = canSprint ? SprintBobFrequency : WalkBobFrequency;
            float bobMul = Mathf.Lerp(1.0f, ExhaustBobMultiplier, exhausted01);

            float s = _t * Mathf.Tau * bobFreq;

            moveY = Mathf.Abs(Mathf.Sin(s)) * bobAmp * bobMul;
            moveTilt = Mathf.Sin(s * 0.5f) * Mathf.DegToRad(BobTiltAmplitudeDeg) * bobMul;

            moveY *= speed01;
            moveTilt *= speed01;
        }

        Vector3 movePos = new Vector3(0.0f, moveY, 0.0f);
        Vector3 moveRot = new Vector3(0.0f, 0.0f, moveTilt);

        Vector3 targetPos = _basePos + breathPos + movePos;
        Vector3 targetRot = _baseRot + breathRot + moveRot;

        float a = 1.0f - Mathf.Exp(-BobBlendSpeed * dt);
        _cameraBob.Position = _cameraBob.Position.Lerp(targetPos, a);
        _cameraBob.Rotation = _cameraBob.Rotation.Lerp(targetRot, a);
    }
}

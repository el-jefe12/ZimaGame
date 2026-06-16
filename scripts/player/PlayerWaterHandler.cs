using Godot;

public partial class PlayerWaterHandler : Node
{
    [Export] public CharacterBody3D _player;
    [Export] public StatusEffectOverlay StatusOverlay;

    [Export] public uint GroundCollisionMask = 1;
    [Export] public float GroundRayStartHeight = 2.0f;
    [Export] public float GroundRayLength = 8.0f;

    [ExportGroup("Cold Water Damage")]

    // Small contact threshold so the player is not damaged by barely touching the water.
    [Export] public float MinimumDamageDepth = 0.05f;

    [ExportGroup("Freezing UI")]

    // The overlay strength while touching freezing water.
    [Export] public float FreezingContactIntensity = 0.75f;

    // If true, deeper water makes the effect slightly stronger.
    [Export] public bool ScaleFreezingEffectWithDepth = false;

    private PlayerHealthSystem _health;

    private WaterBodyScript _currentWaterBody;

    public bool IsInWater => _currentWaterBody != null;
    public bool IsTouchingFreezingWater { get; private set; } = false;
    public bool IsTooDeep { get; private set; } = false;
    public float CurrentWaterDepth { get; private set; } = 0.0f;

    public float MovementMultiplier
    {
        get
        {
            if (_currentWaterBody == null)
                return 1.0f;

            if (CurrentWaterDepth <= MinimumDamageDepth)
                return 1.0f;

            return _currentWaterBody.MovementSlowMultiplier;
        }
    }

    public override void _Ready()
    {
        _health = GetNodeOrNull<PlayerHealthSystem>("../PlayerHealth");

        if (_player == null)
        {
            GD.PushError("PlayerWaterHandler: _player is not assigned.");
            SetPhysicsProcess(false);
            return;
        }

        if (_health == null)
        {
            GD.PushWarning("PlayerWaterHandler: PlayerHealth node not found. Cold water damage disabled.");
        }

        ClearFreezingOverlay();
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;

        if (_currentWaterBody == null)
        {
            CurrentWaterDepth = 0.0f;
            IsTooDeep = false;
            IsTouchingFreezingWater = false;

            ClearFreezingOverlay();
            return;
        }

        CurrentWaterDepth = GetWaterDepthAtPosition(_player.GlobalPosition);
        IsTooDeep = CurrentWaterDepth >= _currentWaterBody.MaxAllowedDepth;

        IsTouchingFreezingWater =
            _currentWaterBody.IsFreezing &&
            CurrentWaterDepth > MinimumDamageDepth;

        UpdateFreezingOverlay();
        TickColdWaterDamage(dt);
    }

    public void EnterWaterBody(WaterBodyScript waterBody)
    {
        _currentWaterBody = waterBody;
    }

    public void ExitWaterBody(WaterBodyScript waterBody)
    {
        if (_currentWaterBody != waterBody)
            return;

        _currentWaterBody = null;
        CurrentWaterDepth = 0.0f;
        IsTooDeep = false;
        IsTouchingFreezingWater = false;

        ClearFreezingOverlay();
    }

    public bool WouldBeTooDeepAtPosition(Vector3 worldPosition)
    {
        if (_currentWaterBody == null)
            return false;

        float depth = GetWaterDepthAtPosition(worldPosition);

        return depth >= _currentWaterBody.MaxAllowedDepth;
    }

    private void TickColdWaterDamage(float dt)
    {
        if (_currentWaterBody == null)
            return;

        if (!_currentWaterBody.IsFreezing)
            return;

        if (_health == null)
            return;

        if (_health.IsDead)
            return;

        if (CurrentWaterDepth <= MinimumDamageDepth)
            return;

        float damageThisFrame = _currentWaterBody.FreezingDamagePerSecond * dt;

        if (damageThisFrame <= 0.0f)
            return;

        _health.Damage(damageThisFrame, false);
    }

    private void UpdateFreezingOverlay()
    {
        if (StatusOverlay == null)
            return;

        if (!IsTouchingFreezingWater)
        {
            StatusOverlay.SetFreezingIntensity(0.0f);
            return;
        }

        float intensity = FreezingContactIntensity;

        if (ScaleFreezingEffectWithDepth && _currentWaterBody != null)
        {
            float depthRatio = CurrentWaterDepth / _currentWaterBody.MaxAllowedDepth;
            depthRatio = Mathf.Clamp(depthRatio, 0.0f, 1.0f);

            intensity = Mathf.Lerp(
                FreezingContactIntensity * 0.5f,
                FreezingContactIntensity,
                depthRatio
            );
        }

        StatusOverlay.SetFreezingIntensity(intensity);
    }

    private void ClearFreezingOverlay()
    {
        if (StatusOverlay == null)
            return;

        StatusOverlay.SetFreezingIntensity(0.0f);
    }

    private float GetWaterDepthAtPosition(Vector3 worldPosition)
    {
        if (_currentWaterBody == null)
            return 0.0f;

        PhysicsDirectSpaceState3D spaceState = _player.GetWorld3D().DirectSpaceState;

        Vector3 rayStart = new Vector3(
            worldPosition.X,
            worldPosition.Y + GroundRayStartHeight,
            worldPosition.Z
        );

        Vector3 rayEnd = rayStart + Vector3.Down * GroundRayLength;

        PhysicsRayQueryParameters3D query =
            PhysicsRayQueryParameters3D.Create(rayStart, rayEnd);

        query.CollisionMask = GroundCollisionMask;
        query.CollideWithAreas = false;
        query.CollideWithBodies = true;
        query.Exclude = new Godot.Collections.Array<Rid> { _player.GetRid() };

        Godot.Collections.Dictionary result = spaceState.IntersectRay(query);

        if (result.Count == 0)
            return 0.0f;

        Vector3 groundPosition = (Vector3)result["position"];

        float depth = _currentWaterBody.WaterSurfaceY - groundPosition.Y;

        if (depth < 0.0f)
            return 0.0f;

        return depth;
    }
}
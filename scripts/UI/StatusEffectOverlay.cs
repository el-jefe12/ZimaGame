using Godot;

public partial class StatusEffectOverlay : Control
{
    [Export] public TextureRect FreezingEffect;
    [Export] public TextureRect InjuredEffect;

    [ExportGroup("Health Connection")]
    [Export] public PlayerHealthSystem HealthSystem;

    [ExportGroup("Injury Hit Flash")]
    [Export] public float InjuryFlashDuration = 0.25f;
    [Export] public float InjuryFlashIntensity = 0.9f;

    // Bigger damage creates stronger flash.
    // Example: 25 damage / 100 max health = 0.25 * 4 = 1.0 intensity.
    [Export] public float InjuryDamageFlashMultiplier = 4.0f;

    // Minimum visible flash even for small damage.
    [Export] public float InjuryMinimumFlashIntensity = 0.35f;

    [ExportGroup("Fade")]
    [Export] public float FadeInSpeed = 10.0f;
    [Export] public float FadeOutSpeed = 5.0f;

    [ExportGroup("Freezing Flash")]
    [Export] public float FreezingFlashSpeed = 3.0f;
    [Export] public float FreezingMinFlashMultiplier = 0.25f;
    [Export] public float FreezingMaxFlashMultiplier = 1.0f;

    [ExportGroup("Injured Flash")]
    [Export] public float InjuredFlashSpeed = 2.0f;
    [Export] public float InjuredMinFlashMultiplier = 0.25f;
    [Export] public float InjuredMaxFlashMultiplier = 1.0f;

    private float _injuryFlashTimer = 0.0f;

    private float _freezingTargetAlpha = 0.0f;
    private float _injuredTargetAlpha = 0.0f;

    private float _freezingAlpha = 0.0f;
    private float _injuredAlpha = 0.0f;

    private float _time = 0.0f;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        ZIndex = 100;

        SetupTextureRect(FreezingEffect);
        SetupTextureRect(InjuredEffect);

        ConnectHealthSystem();
    }

    public override void _ExitTree()
    {
        DisconnectHealthSystem();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _time += dt;

        TickInjuryFlash(dt);

        _freezingAlpha = MoveAlpha(_freezingAlpha, _freezingTargetAlpha, dt);
        _injuredAlpha = MoveAlpha(_injuredAlpha, _injuredTargetAlpha, dt);

        ApplyFlashingAlpha(
            FreezingEffect,
            _freezingAlpha,
            FreezingFlashSpeed,
            FreezingMinFlashMultiplier,
            FreezingMaxFlashMultiplier
        );

        ApplyFlashingAlpha(
            InjuredEffect,
            _injuredAlpha,
            InjuredFlashSpeed,
            InjuredMinFlashMultiplier,
            InjuredMaxFlashMultiplier
        );
    }

    public void SetFreezingIntensity(float intensity01)
    {
        _freezingTargetAlpha = Mathf.Clamp(intensity01, 0.0f, 1.0f);
    }

    // Use this later for lasting effects like bleeding.
    // For normal damage, use FlashInjury() instead.
    public void SetInjuryIntensity(float intensity01)
    {
        _injuredTargetAlpha = Mathf.Clamp(intensity01, 0.0f, 1.0f);
    }

    public void FlashInjury(float intensity01 = 1.0f)
    {
        float intensity = Mathf.Clamp(intensity01, 0.0f, 1.0f);

        _injuryFlashTimer = InjuryFlashDuration;
        _injuredTargetAlpha = Mathf.Max(
            _injuredTargetAlpha,
            InjuryFlashIntensity * intensity
        );
    }

    public void ClearFreezing()
    {
        _freezingTargetAlpha = 0.0f;
    }

    public void ClearInjury()
    {
        _injuryFlashTimer = 0.0f;
        _injuredTargetAlpha = 0.0f;
    }

    public void ClearAll()
    {
        _freezingTargetAlpha = 0.0f;

        _injuryFlashTimer = 0.0f;
        _injuredTargetAlpha = 0.0f;
    }

	private void ConnectHealthSystem()
	{
		if (HealthSystem == null)
		{
			GD.PushWarning("StatusEffectOverlay: HealthSystem is not assigned.");
			return;
		}

		HealthSystem.DamageTaken -= OnDamageTaken;
		HealthSystem.DamageTaken += OnDamageTaken;

		GD.Print("StatusEffectOverlay: Connected to PlayerHealthSystem.DamageTaken.");
	}

    private void DisconnectHealthSystem()
    {
        if (HealthSystem == null)
            return;

        HealthSystem.DamageTaken -= OnDamageTaken;
    }

	private void OnDamageTaken(float damage, float current, float max)
	{
		GD.Print($"[StatusEffectOverlay] DamageTaken received: {damage}");

		if (max <= 0.0f)
			return;

		float damageRatio = damage / max;

		float intensity = damageRatio * InjuryDamageFlashMultiplier;
		intensity = Mathf.Clamp(
			intensity,
			InjuryMinimumFlashIntensity,
			1.0f
		);

		FlashInjury(intensity);
	}

    private void TickInjuryFlash(float dt)
    {
        if (_injuryFlashTimer <= 0.0f)
        {
            _injuredTargetAlpha = 0.0f;
            return;
        }

        _injuryFlashTimer -= dt;

        if (_injuryFlashTimer <= 0.0f)
            _injuredTargetAlpha = 0.0f;
    }

    private void SetupTextureRect(TextureRect textureRect)
    {
        if (textureRect == null)
            return;

        textureRect.MouseFilter = MouseFilterEnum.Ignore;
        textureRect.SetAnchorsPreset(LayoutPreset.FullRect);
        textureRect.StretchMode = TextureRect.StretchModeEnum.Scale;

        Color color = textureRect.Modulate;
        color.A = 0.0f;
        textureRect.Modulate = color;
    }

    private float MoveAlpha(float current, float target, float dt)
    {
        float speed = target > current ? FadeInSpeed : FadeOutSpeed;
        return Mathf.MoveToward(current, target, speed * dt);
    }

    private void ApplyFlashingAlpha(
        TextureRect textureRect,
        float baseAlpha,
        float flashSpeed,
        float minMultiplier,
        float maxMultiplier
    )
    {
        if (textureRect == null)
            return;

        if (baseAlpha <= 0.001f)
        {
            Color hiddenColor = textureRect.Modulate;
            hiddenColor.A = 0.0f;
            textureRect.Modulate = hiddenColor;
            return;
        }

        // Uses TAU so flashSpeed behaves like "flashes per second".
        float wave01 = (Mathf.Sin(_time * Mathf.Tau * flashSpeed) + 1.0f) * 0.5f;

        float multiplier = Mathf.Lerp(
            minMultiplier,
            maxMultiplier,
            wave01
        );

        float finalAlpha = baseAlpha * multiplier;
        finalAlpha = Mathf.Clamp(finalAlpha, 0.0f, 1.0f);

        Color color = textureRect.Modulate;
        color.A = finalAlpha;
        textureRect.Modulate = color;
    }
}
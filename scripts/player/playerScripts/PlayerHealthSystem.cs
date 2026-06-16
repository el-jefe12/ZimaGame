using Godot;
using System;

public partial class PlayerHealthSystem : Node
{
    [Signal] public delegate void HealthChangedEventHandler(float current, float max);
    [Signal] public delegate void DamageTakenEventHandler(float damage, float current, float max);
    [Signal] public delegate void PlayerDiedEventHandler();

    [Export] public float MaxHealth = 100.0f;
    [Export] public float Health = 100.0f;

    private float _lastHealth = -9999.0f;

    private bool _isDead = false;
    public bool IsDead => _isDead;

    public override void _Ready()
    {
        Health = Mathf.Clamp(Health, 0.0f, MaxHealth);
        _lastHealth = Health;

        EmitSignal(SignalName.HealthChanged, Health, MaxHealth);

        GD.Print($"[HealthSystem] Ready with Health = {Health}");
    }

    public void Damage(float dmg, bool triggerInjuryFlash = true)
    {
        if (_isDead)
            return;

        if (dmg <= 0.0f)
            return;

        GD.Print($"[HealthSystem] Damage called: {dmg}");

        float previousHealth = Health;

        Health -= dmg;
        Health = Mathf.Clamp(Health, 0.0f, MaxHealth);

        float actualDamage = previousHealth - Health;

        NotifyIfChanged();

        if (triggerInjuryFlash && actualDamage > 0.0f)
        {
            GD.Print($"[HealthSystem] Emitting DamageTaken: {actualDamage}");
            EmitSignal(SignalName.DamageTaken, actualDamage, Health, MaxHealth);
        }

        if (Health <= 0.0f)
        {
            Die();
        }
    }

    public void Heal(float amount)
    {
        if (_isDead)
            return;

        if (amount <= 0.0f)
            return;

        Health += amount;
        Health = Mathf.Clamp(Health, 0.0f, MaxHealth);

        NotifyIfChanged();
    }

    public void SetHealth(float value, bool triggerInjuryFlash = true)
    {
        if (_isDead)
            return;

        float previousHealth = Health;

        Health = Mathf.Clamp(value, 0.0f, MaxHealth);

        NotifyIfChanged();

        float actualDamage = previousHealth - Health;

        if (triggerInjuryFlash && actualDamage > 0.0f)
        {
            EmitSignal(SignalName.DamageTaken, actualDamage, Health, MaxHealth);
        }

        if (Health <= 0.0f)
        {
            Die();
        }
    }

    private void NotifyIfChanged()
    {
        if (!Mathf.IsEqualApprox(Health, _lastHealth))
        {
            _lastHealth = Health;
            EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
        }

        GD.Print($"[HealthSystem] Health now: {Health}");
    }

    private void Die()
    {
        if (_isDead)
            return;

        _isDead = true;

        GD.Print("[HealthSystem] Player died");

        EmitSignal(SignalName.PlayerDied);
    }
}
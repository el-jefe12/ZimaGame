using Godot;

public partial class Damageable : Node
{
    [Signal] public delegate void DamagedEventHandler(float damage, float currentHealth, float maxHealth);
    [Signal] public delegate void DiedEventHandler();

    [ExportCategory("Health")]
    [Export] public float MaxHealth = 50.0f;

    [Export] public bool Destroyed = false;

    private float _health;

    public float Health => _health;

    public override void _Ready()
    {
        _health = MaxHealth;
    }

    // Your AttackAction can call this.
    public void TakeDamage(float damage)
    {
        if (Destroyed)
        {
            return;
        }

        if (damage <= 0.0f)
        {
            return;
        }

        _health -= damage;

        GD.Print($"Damageable: Took {damage} damage. Health: {_health}/{MaxHealth}");

        EmitSignal(SignalName.Damaged, damage, _health, MaxHealth);

        if (_health <= 0.0f)
        {
            Die();
        }
    }

    public void Heal(float amount)
    {
        if (Destroyed)
        {
            return;
        }

        if (amount <= 0.0f)
        {
            return;
        }

        _health = Mathf.Min(_health + amount, MaxHealth);
    }

    public void ResetHealth()
    {
        Destroyed = false;
        _health = MaxHealth;
    }

    private void Die()
    {
        if (Destroyed)
        {
            return;
        }

        Destroyed = true;
        _health = 0.0f;

        GD.Print("Damageable: Died.");

        EmitSignal(SignalName.Died);
    }
}
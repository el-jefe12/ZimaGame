using Godot;
using System;

public partial class PlayerStats : Node
{
	[Signal] public delegate void StaminaChangedEventHandler(float current, float max);

	[Export] public float MaxStamina = 100.0f;
	[Export] public float Stamina = 100.0f;

	[Export] public float SprintDrainPerSecond = 10.0f;
	[Export] public float WalkDrainPerSecond = 0.5f;

	[Export] public float JumpStaminaCost = 15.0f;

	[Export] public float StaminaRegenPerSecond = 14.0f;
	[Export] public float RegenDelaySeconds = 2.5f;

	[Export] public float ExhaustedThreshold = 0.01f;
	[Export] public float ExhaustedCancelSprintTreshold = 30f;
	[Export] public float ExhaustedSlowWalkTreshold = 15f;

	private float _regenCooldown = 0.0f;
	private float _lastStamina = -99999.0f;

	public override void _Ready()
	{
		SetStamina(Stamina, true);
	}

	public bool CanMove()
	{
		return Stamina > ExhaustedThreshold;
	}

	public bool CanSprint()
	{
		return Stamina > ExhaustedCancelSprintTreshold;
	}

	public bool IsSlowWalk()
	{
		return Stamina <= ExhaustedSlowWalkTreshold;
	}

	public bool TryJump()
	{
		if (Stamina < JumpStaminaCost)
		{
			return false;
		}

		Stamina -= JumpStaminaCost;
		Stamina = Mathf.Clamp(Stamina, 0.0f, MaxStamina);

		_regenCooldown = RegenDelaySeconds;

		NotifyIfChanged();

		return true;
	}

	public void TickStamina(float dt, bool wantsMove, bool sprinting)
	{
		bool staminaUsed = false;

		if (wantsMove && CanMove())
		{
			float drain = (sprinting ? SprintDrainPerSecond : WalkDrainPerSecond) * dt;

			Stamina -= drain;
			Stamina = Mathf.Clamp(Stamina, 0.0f, MaxStamina);

			_regenCooldown = RegenDelaySeconds;
			staminaUsed = true;
		}

		if (!staminaUsed)
		{
			if (_regenCooldown > 0.0f)
			{
				_regenCooldown -= dt;

				if (_regenCooldown < 0.0f)
				{
					_regenCooldown = 0.0f;
				}
			}
			else
			{
				if (Stamina < MaxStamina)
				{
					Stamina += StaminaRegenPerSecond * dt;
					Stamina = Mathf.Clamp(Stamina, 0.0f, MaxStamina);
				}
			}
		}

		NotifyIfChanged();
	}

	public void SetStamina(float value, bool emitNow = true)
	{
		Stamina = Mathf.Clamp(value, 0.0f, MaxStamina);

		if (emitNow)
		{
			_lastStamina = Stamina;
			EmitSignal(SignalName.StaminaChanged, Stamina, MaxStamina);
		}
	}

	public void AddStamina(float amount)
	{
		Stamina = Mathf.Clamp(Stamina + amount, 0.0f, MaxStamina);

		// Negative stamina effects should delay normal regen.
		if (amount < 0.0f)
		{
			_regenCooldown = RegenDelaySeconds;
		}

		NotifyIfChanged();
	}

	private void NotifyIfChanged()
	{
		if (!Mathf.IsEqualApprox(Stamina, _lastStamina))
		{
			_lastStamina = Stamina;
			EmitSignal(SignalName.StaminaChanged, Stamina, MaxStamina);
		}
	}
}
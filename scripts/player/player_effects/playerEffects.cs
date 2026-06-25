using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class PlayerEffects : Node
{
	[ExportCategory("References")]
	[Export] public NodePath PlayerHealthPath = "%PlayerHealth";
	[Export] public NodePath PlayerStatsPath = "%PlayerStats";

	private PlayerHealthSystem? _health;
	private PlayerStats? _stats;

	private readonly List<ActiveEffect> _activeEffects = new();

	public override void _Ready()
	{
		_health = GetNodeOrNull<PlayerHealthSystem>(PlayerHealthPath);
		_stats = GetNodeOrNull<PlayerStats>(PlayerStatsPath);

		if (_health == null)
		{
			GD.PushError("PlayerEffects: PlayerHealthSystem not found.");
		}

		if (_stats == null)
		{
			GD.PushWarning("PlayerEffects: PlayerStats not found. Stamina effects may not work.");
		}
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		TickActiveEffects(dt);
	}

	public void ApplyEffect(Effect effect)
	{
		if (effect == null)
		{
			GD.PushError("PlayerEffects: Tried to apply a null effect.");
			return;
		}

		ActiveEffect? existingEffect = FindExistingEffect(effect);

		if (existingEffect != null)
		{
			if (effect.EffectStackMode == Effect.StackMode.IgnoreIfAlreadyActive)
			{
				return;
			}

			if (effect.EffectStackMode == Effect.StackMode.RefreshDuration)
			{
				existingEffect.RefreshDuration();
				GD.Print($"PlayerEffects: Refreshed effect '{effect.EffectName}'.");
				return;
			}
		}

		ActiveEffect activeEffect = new ActiveEffect(effect);
		_activeEffects.Add(activeEffect);

		ApplyModifiersByTiming(activeEffect, EffectModifier.ModifierTiming.OnApply);

		GD.Print($"PlayerEffects: Applied effect '{effect.EffectName}'.");
	}

	public void RemoveEffectById(string effectId)
	{
		for (int i = _activeEffects.Count - 1; i >= 0; i--)
		{
			ActiveEffect activeEffect = _activeEffects[i];

			if (activeEffect.Effect.EffectId != effectId)
			{
				continue;
			}

			RemoveEffectAt(i);
		}
	}

	public bool HasEffect(string effectId)
	{
		foreach (ActiveEffect activeEffect in _activeEffects)
		{
			if (activeEffect.Effect.EffectId == effectId)
			{
				return true;
			}
		}

		return false;
	}

	public float ApplyPassiveModifiers(EffectModifier.ModifierTarget target, float baseValue)
	{
		float result = baseValue;

		List<EffectModifier> passiveModifiers = GetPassiveModifiersForTarget(target);

		foreach (EffectModifier modifier in passiveModifiers)
		{
			result = ApplyMathOperation(result, modifier.Operation, modifier.Value);
		}

		return result;
	}

	private void TickActiveEffects(float dt)
	{
		for (int i = _activeEffects.Count - 1; i >= 0; i--)
		{
			ActiveEffect activeEffect = _activeEffects[i];

			TickOverTimeModifiers(activeEffect, dt);

			if (!activeEffect.Effect.IsPermanent)
			{
				activeEffect.RemainingSeconds -= dt;

				if (activeEffect.RemainingSeconds <= 0.0f)
				{
					RemoveEffectAt(i);
				}
			}
		}
	}

	private void TickOverTimeModifiers(ActiveEffect activeEffect, float dt)
	{
		List<EffectModifier> modifiers = activeEffect.TickTimers.Keys.ToList();

		foreach (EffectModifier modifier in modifiers)
		{
			float timer = activeEffect.TickTimers[modifier];
			timer += dt;

			float safeTickInterval = Mathf.Max(0.01f, modifier.TickInterval);

			while (timer >= safeTickInterval)
			{
				timer -= safeTickInterval;
				ApplyModifier(modifier);
			}

			activeEffect.TickTimers[modifier] = timer;
		}
	}

	private void RemoveEffectAt(int index)
	{
		ActiveEffect activeEffect = _activeEffects[index];

		ApplyModifiersByTiming(activeEffect, EffectModifier.ModifierTiming.OnRemove);

		GD.Print($"PlayerEffects: Removed effect '{activeEffect.Effect.EffectName}'.");

		_activeEffects.RemoveAt(index);
	}

	private ActiveEffect? FindExistingEffect(Effect effect)
	{
		foreach (ActiveEffect activeEffect in _activeEffects)
		{
			bool sameResource = activeEffect.Effect == effect;

			bool sameId =
				!string.IsNullOrWhiteSpace(effect.EffectId)
				&& activeEffect.Effect.EffectId == effect.EffectId;

			if (sameResource || sameId)
			{
				return activeEffect;
			}
		}

		return null;
	}

	private void ApplyModifiersByTiming(ActiveEffect activeEffect, EffectModifier.ModifierTiming timing)
	{
		foreach (EffectModifier modifier in activeEffect.Effect.Modifiers)
		{
			if (modifier == null)
			{
				continue;
			}

			if (modifier.Timing != timing)
			{
				continue;
			}

			ApplyModifier(modifier);
		}
	}

	private List<EffectModifier> GetPassiveModifiersForTarget(EffectModifier.ModifierTarget target)
	{
		List<EffectModifier> modifiers = new();

		foreach (ActiveEffect activeEffect in _activeEffects)
		{
			foreach (EffectModifier modifier in activeEffect.Effect.Modifiers)
			{
				if (modifier == null)
				{
					continue;
				}

				if (modifier.Timing != EffectModifier.ModifierTiming.Passive)
				{
					continue;
				}

				if (modifier.Target != target)
				{
					continue;
				}

				modifiers.Add(modifier);
			}
		}

		modifiers.Sort((a, b) => a.Priority.CompareTo(b.Priority));

		return modifiers;
	}

	private void ApplyModifier(EffectModifier modifier)
	{
		switch (modifier.Target)
		{
			case EffectModifier.ModifierTarget.Health:
				ApplyHealthModifier(modifier);
				break;

			case EffectModifier.ModifierTarget.Stamina:
				ApplyStaminaModifier(modifier);
				break;

			default:
				GD.Print($"PlayerEffects: Modifier target '{modifier.Target}' is not directly implemented yet.");
				break;
		}
	}

	private void ApplyHealthModifier(EffectModifier modifier)
	{
		if (_health == null)
		{
			return;
		}

		if (modifier.Operation != EffectModifier.ModifierOperation.Add)
		{
			GD.Print($"PlayerEffects: Health operation '{modifier.Operation}' is not implemented yet.");
			return;
		}

		float value = modifier.Value;

		if (value < 0.0f)
		{
			_health.Damage(Mathf.Abs(value));
		}
		else if (value > 0.0f)
		{
			_health.Heal(value);
		}
	}

	private void ApplyStaminaModifier(EffectModifier modifier)
	{
		if (_stats == null)
		{
			return;
		}

		if (modifier.Operation != EffectModifier.ModifierOperation.Add)
		{
			GD.Print($"PlayerEffects: Stamina operation '{modifier.Operation}' is not implemented yet.");
			return;
		}

		_stats.AddStamina(modifier.Value);
	}

	private float ApplyMathOperation(
		float currentValue,
		EffectModifier.ModifierOperation operation,
		float modifierValue
	)
	{
		switch (operation)
		{
			case EffectModifier.ModifierOperation.Add:
				return currentValue + modifierValue;

			case EffectModifier.ModifierOperation.Multiply:
				return currentValue * modifierValue;

			case EffectModifier.ModifierOperation.CapMultiplier:
				return currentValue * modifierValue;

			case EffectModifier.ModifierOperation.Set:
				return modifierValue;

			default:
				return currentValue;
		}
	}
}
using Godot;
using System.Collections.Generic;

public sealed class ActiveEffect
{
	public Effect Effect { get; private set; }

	public float RemainingSeconds { get; set; }

	// Each OverTime modifier gets its own timer.
	public Dictionary<EffectModifier, float> TickTimers { get; private set; } = new();

	public ActiveEffect(Effect effect)
	{
		Effect = effect;

		RemainingSeconds = effect.IsPermanent
			? float.PositiveInfinity
			: effect.DurationSeconds;

		foreach (EffectModifier modifier in effect.Modifiers)
		{
			if (modifier == null)
			{
				continue;
			}

			if (modifier.Timing == EffectModifier.ModifierTiming.OverTime)
			{
				TickTimers[modifier] = 0.0f;
			}
		}
	}

	public void RefreshDuration()
	{
		if (Effect.IsPermanent)
		{
			RemainingSeconds = float.PositiveInfinity;
			return;
		}

		RemainingSeconds = Effect.DurationSeconds;
	}
}
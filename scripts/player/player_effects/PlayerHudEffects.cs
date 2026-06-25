using System.Collections.Generic;
using Godot;

public partial class PlayerHudEffects : Node
{
	private enum BadDirection
	{
		LowValueIsBad = 0,
		HighValueIsBad = 1
	}

	private VBoxContainer _effectsList = null!;
	private HBoxContainer _effectsHBoxContainer = null!;

	private PanelContainer _dummyEffectSlot = null!;
	private Control _dummyEffectIcon = null!;

	private PlayerEffects? _playerEffectsSystem;

	private List<Effect> _allEffects = new();

	private readonly List<Effect> _visibleEffects = new();

	private readonly Dictionary<Effect.EType, int> _activeSeverityByType = new();
	private readonly Dictionary<Effect.EType, Control> _activeIconsByType = new();
	private readonly Dictionary<Effect.EType, Control> _activeSlotsByType = new();
	private readonly Dictionary<Effect.EType, Effect> _activeEffectByType = new();

	// Your PlayerNeedsSystem drains ALL THREE downward:
	// 100 = good, 0 = bad.
	private readonly Dictionary<Effect.EType, BadDirection> _badDirectionByType = new()
	{
		[Effect.EType.Hunger] = BadDirection.LowValueIsBad,
		[Effect.EType.Thirst] = BadDirection.LowValueIsBad,
		[Effect.EType.Tired] = BadDirection.LowValueIsBad,
	};

	// Badness thresholds:
	// 0 = fine, 100 = terrible.
	private const float LowBadness = 25.0f;
	private const float MediumBadness = 50.0f;
	private const float HighBadness = 75.0f;
	private const float DeadlyBadness = 90.0f;

	public IReadOnlyList<Effect> PlayerEffects => _visibleEffects;

	public void Setup(
		VBoxContainer effectsList,
		HBoxContainer effectsHBoxContainer,
		PanelContainer dummyEffectSlot,
		Control dummyEffectIcon,
		PlayerEffects? playerEffectsSystem
	)
	{
		_effectsList = effectsList;
		_effectsHBoxContainer = effectsHBoxContainer;
		_dummyEffectSlot = dummyEffectSlot;
		_dummyEffectIcon = dummyEffectIcon;
		_playerEffectsSystem = playerEffectsSystem;

		if (EffectListScript.Instance == null)
		{
			GD.PushError("PlayerHudEffects: EffectListScript.Instance is NULL. Check your autoload setup.");
			return;
		}

		_allEffects = EffectListScript.Instance.EffectsList;

		EffectListScript.Instance.EnsureLoaded();

		_allEffects = EffectListScript.Instance.EffectsList;

		GD.Print($"PlayerHudEffects: loaded effects count = {_allEffects.Count}");
		PrintAllLoadedEffects();

		if (_allEffects.Count == 0)
		{
			GD.PushError(
				"PlayerHudEffects: Effect list is still empty after EnsureLoaded(). " +
				"Fill EffectListScript.ExportEffects in the autoload inspector."
			);
		}

		if (_playerEffectsSystem == null)
		{
			GD.PushWarning("PlayerHudEffects: PlayerEffects system is null. HUD effects will show, but gameplay modifiers will not run.");
		}
	}

	public void HandleStatEffect(Effect.EType type, float current, float max)
	{
		if (max <= 0.0f)
		{
			GD.PushWarning($"PlayerHudEffects: Invalid max value for {type}. Max={max}");
			return;
		}

		float safeCurrent = Mathf.Clamp(current, 0.0f, max);
		float valuePercent = safeCurrent / max * 100.0f;
		float badnessPercent = GetBadnessPercent(type, valuePercent);

		int newSeverity = BadnessToSeverity(badnessPercent);
		int oldSeverity = GetActiveSeverity(type);

		GD.Print(
			$"PlayerHudEffects: Type={type}, Current={current:F1}, Safe={safeCurrent:F1}, Max={max:F1}, " +
			$"ValuePercent={valuePercent:F1}, Badness={badnessPercent:F1}, Old={oldSeverity}, New={newSeverity}"
		);

		if (newSeverity == oldSeverity)
		{
			return;
		}

		if (oldSeverity != -1)
		{
			RemoveEffect(type);
			_activeSeverityByType.Remove(type);
		}

		if (newSeverity == -1)
		{
			return;
		}

		Effect? effect = FindEffect(type, newSeverity);

		if (effect == null)
		{
			GD.PushWarning($"PlayerHudEffects: Could not find effect resource for Type={type}, Severity={newSeverity}.");
			return;
		}

		bool added = AddEffect(effect);

		if (added)
		{
			_activeSeverityByType[type] = newSeverity;
		}
	}

	public void ClearAllEffects()
	{
		foreach (Effect.EType type in new List<Effect.EType>(_activeSeverityByType.Keys))
		{
			RemoveEffect(type);
		}

		_activeSeverityByType.Clear();
		_visibleEffects.Clear();
		_activeEffectByType.Clear();
	}

	private float GetBadnessPercent(Effect.EType type, float valuePercent)
	{
		BadDirection direction = BadDirection.LowValueIsBad;

		if (_badDirectionByType.TryGetValue(type, out BadDirection foundDirection))
		{
			direction = foundDirection;
		}

		if (direction == BadDirection.LowValueIsBad)
		{
			return 100.0f - valuePercent;
		}

		return valuePercent;
	}

	private int BadnessToSeverity(float badnessPercent)
	{
		if (badnessPercent >= DeadlyBadness)
		{
			GD.Print("PlayerHudEffects: Severity = DEADLY");
			return (int)Effect.Severity.Deadly;
		}

		if (badnessPercent >= HighBadness)
		{
			GD.Print("PlayerHudEffects: Severity = HIGH");
			return (int)Effect.Severity.High;
		}

		if (badnessPercent >= MediumBadness)
		{
			GD.Print("PlayerHudEffects: Severity = MEDIUM");
			return (int)Effect.Severity.Medium;
		}

		if (badnessPercent >= LowBadness)
		{
			GD.Print("PlayerHudEffects: Severity = LOW");
			return (int)Effect.Severity.Low;
		}

		GD.Print("PlayerHudEffects: Severity = NONE");
		return -1;
	}

	private int GetActiveSeverity(Effect.EType type)
	{
		if (_activeSeverityByType.TryGetValue(type, out int severity))
		{
			return severity;
		}

		return -1;
	}

	private Effect? FindEffect(Effect.EType type, int severity)
	{
		foreach (Effect effect in _allEffects)
		{
			if (effect == null)
			{
				continue;
			}

			if (effect.Type == type && (int)effect.EffectSeverity == severity)
			{
				string iconPath = effect.Icon == null ? "NULL" : effect.Icon.ResourcePath;

				GD.Print(
					$"PlayerHudEffects: Found effect '{effect.EffectName}' for Type={type}, " +
					$"Severity={severity}, Icon='{iconPath}'"
				);

				return effect;
			}
		}

		GD.PushWarning($"PlayerHudEffects: effect not found. Type={type}, Severity={severity}, Effects loaded={_allEffects.Count}");
		PrintAvailableEffectsForType(type);
		return null;
	}

	private void PrintAvailableEffectsForType(Effect.EType type)
	{
		GD.Print($"PlayerHudEffects: Available effects for Type={type}:");

		foreach (Effect effect in _allEffects)
		{
			if (effect == null)
			{
				continue;
			}

			if (effect.Type != type)
			{
				continue;
			}

			string iconPath = effect.Icon == null ? "NULL" : effect.Icon.ResourcePath;

			GD.Print(
				$" - Name='{effect.EffectName}', Severity={effect.EffectSeverity} ({(int)effect.EffectSeverity}), " +
				$"Id='{effect.EffectId}', Icon='{iconPath}'"
			);
		}
	}

	private void PrintAllLoadedEffects()
	{
		GD.Print("========== PLAYER HUD EFFECTS LOADED ==========");

		foreach (Effect effect in _allEffects)
		{
			if (effect == null)
			{
				GD.Print("Effect: NULL");
				continue;
			}

			string iconPath = effect.Icon == null ? "NULL" : effect.Icon.ResourcePath;

			int modifierCount = effect.Modifiers == null ? 0 : effect.Modifiers.Count;

			GD.Print(
				$"Effect: Name='{effect.EffectName}', Type={effect.Type}, " +
				$"Severity={effect.EffectSeverity} ({(int)effect.EffectSeverity}), " +
				$"Id='{effect.EffectId}', Modifiers={modifierCount}, Icon='{iconPath}'"
			);
		}

		GD.Print("===============================================");
	}

	private bool AddEffect(Effect effect)
	{
		if (effect == null)
		{
			GD.PushWarning("PlayerHudEffects: AddEffect received null effect.");
			return false;
		}

		if (_effectsList == null || _effectsHBoxContainer == null || _dummyEffectSlot == null || _dummyEffectIcon == null)
		{
			GD.PushError("PlayerHudEffects: Setup references are missing. Did you call Setup()?");
			return false;
		}

		Effect.EType type = effect.Type;

		string iconPath = effect.Icon == null ? "NULL" : effect.Icon.ResourcePath;

		GD.Print(
			$"PlayerHudEffects: AddEffect START. Name='{effect.EffectName}', " +
			$"Type={effect.Type}, Severity={effect.EffectSeverity}, Icon='{iconPath}'"
		);

		EffectWindowLogic slot = (EffectWindowLogic)_dummyEffectSlot
			.Duplicate((int)Node.DuplicateFlags.UseInstantiation);

		slot.Visible = true;

		// Add to tree first, then give it the resource.
		_effectsList.AddChild(slot);
		slot.EffectResourceRaw = effect;

		EffectIconLogic iconRoot = (EffectIconLogic)_dummyEffectIcon
			.Duplicate((int)Node.DuplicateFlags.UseInstantiation);

		iconRoot.Visible = true;

		// IMPORTANT:
		// Add to tree first, then give it the resource.
		// This lets EffectIconLogic find its child TextureRect properly.
		_effectsHBoxContainer.AddChild(iconRoot);
		iconRoot.EffectResourceRaw = effect;

		// If your EffectIconLogic has Refresh(), call it after the node is inside the tree.
		if (iconRoot.HasMethod("Refresh"))
		{
			iconRoot.CallDeferred("Refresh");
		}

		_visibleEffects.Add(effect);

		_activeSlotsByType[type] = slot;
		_activeIconsByType[type] = iconRoot;
		_activeEffectByType[type] = effect;

		if (_playerEffectsSystem != null)
		{
			GD.Print($"PlayerHudEffects: Applying gameplay effect '{effect.EffectName}', Type={effect.Type}, Severity={effect.EffectSeverity}, Id='{effect.EffectId}'");
			_playerEffectsSystem.ApplyEffect(effect);
		}

		GD.Print($"PlayerHudEffects: AddEffect DONE. Name='{effect.EffectName}', Icon='{iconPath}'");

		return true;
	}

	private void RemoveEffect(Effect.EType type)
	{
		if (_activeEffectByType.TryGetValue(type, out Effect effectToRemove))
		{
			if (_playerEffectsSystem != null)
			{
				if (!string.IsNullOrWhiteSpace(effectToRemove.EffectId))
				{
					GD.Print($"PlayerHudEffects: Removing gameplay effect '{effectToRemove.EffectName}', Id='{effectToRemove.EffectId}'");
					_playerEffectsSystem.RemoveEffectById(effectToRemove.EffectId);
				}
				else
				{
					GD.PushWarning($"PlayerHudEffects: Cannot remove gameplay effect '{effectToRemove.EffectName}' because EffectId is empty.");
				}
			}
		}

		if (_activeIconsByType.TryGetValue(type, out Control icon) && IsInstanceValid(icon))
		{
			icon.QueueFree();
		}

		if (_activeSlotsByType.TryGetValue(type, out Control slot) && IsInstanceValid(slot))
		{
			slot.QueueFree();
		}

		_activeIconsByType.Remove(type);
		_activeSlotsByType.Remove(type);
		_activeEffectByType.Remove(type);

		_visibleEffects.RemoveAll(effect =>
			effect != null && effect.Type == type
		);
	}
}
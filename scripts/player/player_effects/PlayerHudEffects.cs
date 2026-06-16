using System.Collections.Generic;
using Godot;

public partial class PlayerHudEffects : Node
{
    private VBoxContainer _effectsList;
    private HBoxContainer _effectsHBoxContainer;

    private PanelContainer _dummyEffectSlot;
    private Control _dummyEffectIcon;

    private List<Effect> _effects = new();

    private readonly List<Effect> _playerEffects = new();

    private readonly Dictionary<Effect.EType, int> _activeSeverityByType = new();
    private readonly Dictionary<Effect.EType, Control> _activeIconsByType = new();
    private readonly Dictionary<Effect.EType, Control> _activeSlotsByType = new();

    private readonly Dictionary<Effect.EType, float[]> _thresholdsByType = new()
    {
        [Effect.EType.Hunger] = new[] { 75f, 50f, 25f, 0f },
        [Effect.EType.Thirst] = new[] { 75f, 50f, 25f, 0f },
        [Effect.EType.Tired]  = new[] { 75f, 50f, 25f, 0f },
    };

    public IReadOnlyList<Effect> PlayerEffects => _playerEffects;

    public void Setup(
        VBoxContainer effectsList,
        HBoxContainer effectsHBoxContainer,
        PanelContainer dummyEffectSlot,
        Control dummyEffectIcon
    )
    {
        _effectsList = effectsList;
        _effectsHBoxContainer = effectsHBoxContainer;
        _dummyEffectSlot = dummyEffectSlot;
        _dummyEffectIcon = dummyEffectIcon;

        if (EffectListScript.Instance == null)
        {
            GD.PushError("PlayerHudEffects: EffectListScript.Instance is NULL. Check your autoload setup.");
            return;
        }

        _effects = EffectListScript.Instance.EffectsList;

        GD.Print($"PlayerHudEffects: loaded effects count = {_effects.Count}");
    }

    public void HandleStatEffect(Effect.EType type, float current, float max)
    {
        float pct = max <= 0f ? 100f : current / max * 100f;
        int newSeverity = PercentToSeverity(type, pct);

        int oldSeverity = -1;

        if (_activeSeverityByType.TryGetValue(type, out int activeSeverity))
            oldSeverity = activeSeverity;

        if (newSeverity == oldSeverity)
            return;

        if (oldSeverity != -1)
            RemoveEffect(type);

        if (newSeverity == -1)
        {
            _activeSeverityByType.Remove(type);
            return;
        }

        _activeSeverityByType[type] = newSeverity;
        CreateEffect(type, newSeverity);
    }

    public void ClearAllEffects()
    {
        foreach (Effect.EType type in new List<Effect.EType>(_activeSeverityByType.Keys))
        {
            RemoveEffect(type);
        }

        _activeSeverityByType.Clear();
        _playerEffects.Clear();
    }

    private void CreateEffect(Effect.EType type, int severity)
    {
        GD.Print($"PlayerHudEffects: CreateEffect called. Type={type}, Severity={severity}");

        Effect effect = FindEffect(type, severity);

        if (effect == null)
        {
            GD.PushWarning($"PlayerHudEffects: No Effect resource found for type={type}, severity={severity}");
            return;
        }

        AddEffect(effect);
    }

    private void AddEffect(Effect effect)
    {
        if (effect == null)
        {
            GD.PushWarning("PlayerHudEffects: AddEffect received null effect.");
            return;
        }

        Effect.EType key = effect.Type;

        if (_effectsList == null)
        {
            GD.PushError("PlayerHudEffects: _effectsList is null. Did you call Setup()?");
            return;
        }

        if (_effectsHBoxContainer == null)
        {
            GD.PushError("PlayerHudEffects: _effectsHBoxContainer is null. Did you call Setup()?");
            return;
        }

        if (_dummyEffectSlot == null)
        {
            GD.PushError("PlayerHudEffects: _dummyEffectSlot is null. Did you call Setup()?");
            return;
        }

        if (_dummyEffectIcon == null)
        {
            GD.PushError("PlayerHudEffects: _dummyEffectIcon is null. Did you call Setup()?");
            return;
        }

        RemoveEffect(key);

        _playerEffects.RemoveAll(existingEffect =>
            existingEffect != null && existingEffect.Type == key
        );

        EffectWindowLogic slot = (EffectWindowLogic)_dummyEffectSlot
            .Duplicate((int)Node.DuplicateFlags.UseInstantiation);

        slot.Visible = true;
        slot.EffectResourceRaw = effect;
        _effectsList.AddChild(slot);

        EffectIconLogic iconRoot = (EffectIconLogic)_dummyEffectIcon
            .Duplicate((int)Node.DuplicateFlags.UseInstantiation);

        iconRoot.Visible = true;
        iconRoot.EffectResourceRaw = effect;
        _effectsHBoxContainer.AddChild(iconRoot);

        _playerEffects.Add(effect);

        _activeSlotsByType[key] = slot;
        _activeIconsByType[key] = iconRoot;
    }

    private void RemoveEffect(Effect.EType type)
    {
        if (_activeIconsByType.TryGetValue(type, out Control icon) && IsInstanceValid(icon))
            icon.QueueFree();

        if (_activeSlotsByType.TryGetValue(type, out Control slot) && IsInstanceValid(slot))
            slot.QueueFree();

        _activeIconsByType.Remove(type);
        _activeSlotsByType.Remove(type);

        _playerEffects.RemoveAll(effect =>
            effect != null && effect.Type == type
        );
    }

    private Effect FindEffect(Effect.EType type, int severity)
    {
        foreach (Effect effect in _effects)
        {
            if (effect == null)
                continue;

            if (effect.Type == type && (int)effect.EffectSeverity == severity)
                return effect;
        }

        GD.PushWarning($"PlayerHudEffects: effect not found. Type='{type}', Severity={severity}, Effects loaded={_effects.Count}");
        return null;
    }

    private int PercentToSeverity(Effect.EType type, float pct)
    {
        if (!_thresholdsByType.TryGetValue(type, out float[] thresholds) || thresholds.Length < 4)
            return -1;

        if (pct <= thresholds[3])
            return 3;

        if (pct <= thresholds[2])
            return 2;

        if (pct <= thresholds[1])
            return 1;

        if (pct <= thresholds[0])
            return 0;

        return -1;
    }
}
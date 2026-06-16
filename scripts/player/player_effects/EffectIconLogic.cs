using Godot;

[Tool]
public partial class EffectIconLogic : Control
{
    private TextureRect _EffectIconTexture;
    private Resource _effectResourceRaw;

    [Export(PropertyHint.ResourceType, "Effect")]
    public Resource EffectResourceRaw
    {
        get => _effectResourceRaw;
        set
        {
            _effectResourceRaw = value;

            // V editoru/setteru často nejsme ready → odlož
            if (IsInsideTree())
                CallDeferred(nameof(ApplyIcon));
        }
    }

    public Effect EffectResource => _effectResourceRaw as Effect;

    public override void _Ready()
    {
        _EffectIconTexture = GetNodeOrNull<TextureRect>("%EffectIconTexture");
        ApplyIcon();
    }

    public void ApplyIcon()
    {
        // 1) UI uzel ještě není k dispozici
        if (_EffectIconTexture == null)
            return;

        // 2) resource není přiřazen nebo není správného typu
        var eff = EffectResource;
        if (eff == null)
        {
            _EffectIconTexture.Texture = null;
            return;
        }

        // 3) Icon může být taky null – to je OK, jen se nastaví null
        _EffectIconTexture.Texture = eff.Icon;

        GD.Print($"[EffectIconLogic] editor={Engine.IsEditorHint()} eff={(EffectResource==null?"null":EffectResource.GetType().Name)} icon={(EffectResource?.Icon==null?"null":"OK")}");
    }
}
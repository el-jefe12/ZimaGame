// EffectWindowLogic.cs
using Godot;
using System;

[Tool]
public partial class EffectWindowLogic : PanelContainer
{
    [Export(PropertyHint.Range, "0.05,1.0,0.01")]
    public double SlideDurationSeconds { get; set; } = 0.12;

    private float _descFullHeight = 0.0f;
    private Tween _descTween;

    private Resource _effectResourceRaw;

    // Track resource reference changes (editor) + subscribe to internal changes
    private Resource _lastEffectResourceRaw = null;
    private Effect _subscribedEffect = null;

    // Kept: variables text
    private string _effectVariables = "";

    private TextureRect _effectIcon;
    private RichTextLabel _effectNameLabel;
    private RichTextLabel _effectDescLabel;
    private RichTextLabel _effectEffectLabel;
    private RichTextLabel _effectStatsLabel;
    private MarginContainer _effectDescriptionContainer;

    private TextureButton _effectInfoTextureButton;
    private PanelContainer _effectLabelBackground;

    [Export(PropertyHint.Range, "0,2,0.05")]
    public double HideDelaySeconds { get; set; } = 0.20;

    private Timer _hideTimer;

    private Theme _defaultHeaderTheme;
    private Theme _defaultBodyTheme;

    private bool _buffEffect;

    [Export] public bool infoToggle = false;

    // guard so editor-set exports don’t call ApplyText before nodes exist
    private bool _uiReady = false;

    [Export]
    public bool buff_effect
    {
        get => _buffEffect;
        set
        {
            _buffEffect = value;
            ApplyTheme();
        }
    }

    private Theme _buffHeader;
    [Export]
    public Theme buff_header
    {
        get => _buffHeader;
        set
        {
            _buffHeader = value;
            ApplyTheme();
        }
    }

    private Theme _buffBody;
    [Export]
    public Theme buff_body
    {
        get => _buffBody;
        set
        {
            _buffBody = value;
            ApplyTheme();
        }
    }

    [Export] public Texture2D infoHoverXTexture;
    [Export] public Texture2D infoHoverTexture;

    // variables are still exported and shown in stats label
    [Export(PropertyHint.MultilineText)]
    public string EffectVariables
    {
        get => _effectVariables;
        set
        {
            _effectVariables = value ?? "";
            if (_uiReady) ApplyText();
        }
    }

    // Single resource input (assignable in inspector) + immediate editor update
    [Export(PropertyHint.ResourceType, "Effect")]
    public Resource EffectResourceRaw
    {
        get => _effectResourceRaw;
        set
        {
            _effectResourceRaw = value;
            SubscribeToEffect(EffectResource); // <-- NEW: listen for internal edits
            if (_uiReady) ApplyEffect();       // updates instantly when assigned in editor
        }
    }

    public Effect EffectResource => _effectResourceRaw as Effect;

    public override void _EnterTree()
    {
        // Ensure _Process runs in editor (optional, but keeps reference-change check reliable)
        if (Engine.IsEditorHint())
        {
            ProcessMode = ProcessModeEnum.Always;
            SetProcess(true);
        }

        // Tool-mode friendly: try applying early, but it will no-op until _uiReady
        SubscribeToEffect(EffectResource);
        ApplyEffect();
    }

    public override void _ExitTree()
    {
        // Avoid dangling editor-time handlers
        SubscribeToEffect(null);
    }

    public override void _Ready()
    {
        GD.Print(Name, " EffectWindow size: ", Size);

        _effectIcon = GetNodeOrNull<TextureRect>("%EffectIcon");
        _effectNameLabel = GetNodeOrNull<RichTextLabel>("%EffectName");
        _effectDescLabel = GetNodeOrNull<RichTextLabel>("%EffectDesc");
        _effectEffectLabel = GetNodeOrNull<RichTextLabel>("%EffectEffectLabel");
        _effectStatsLabel = GetNodeOrNull<RichTextLabel>("%EffectStatsLabel");

        _effectDescriptionContainer = GetNodeOrNull<MarginContainer>("%EffectDescriptionContainer");
        _effectLabelBackground = GetNodeOrNull<PanelContainer>("%EffectLabelBackground");
        _effectInfoTextureButton = GetNodeOrNull<TextureButton>("%EffectInfoTextureButton");

        if (_effectNameLabel != null) _effectNameLabel.BbcodeEnabled = true;
        if (_effectDescLabel != null) _effectDescLabel.BbcodeEnabled = true;
        if (_effectStatsLabel != null) _effectStatsLabel.BbcodeEnabled = true;

        if (_effectDescriptionContainer != null)
            _effectDescriptionContainer.Visible = false;

        _defaultHeaderTheme = Theme;
        _defaultBodyTheme = _effectLabelBackground != null ? _effectLabelBackground.Theme : null;

        MouseFilter = MouseFilterEnum.Stop;

        // create timer
        _hideTimer = new Timer();
        _hideTimer.OneShot = true;
        _hideTimer.Autostart = false;
        AddChild(_hideTimer);
        _hideTimer.Timeout += _on_hide_timer_timeout;

        // Mark UI ready after nodes are fetched
        _uiReady = true;

        _lastEffectResourceRaw = EffectResourceRaw;

        // Subscribe now that we're ready too (safe if already subscribed)
        SubscribeToEffect(EffectResource);

        // Pull presentation data from resource
        ApplyEffect();
        ApplyText();
        CallDeferred(nameof(ApplyTheme));
    }

    // Tool script: react when the inspector changes exported values
    public override void _Process(double delta)
    {
        if (!Engine.IsEditorHint())
            return;

        // Detect reference swaps (new resource assigned) even if setter didn't run for some reason
        if (!ReferenceEquals(EffectResourceRaw, _lastEffectResourceRaw))
        {
            _lastEffectResourceRaw = EffectResourceRaw;

            SubscribeToEffect(EffectResource); // <-- NEW
            ApplyEffect();
        }
    }

    // NEW: subscribe/unsubscribe to the resource's Changed event
    private void SubscribeToEffect(Effect eff)
    {
        if (ReferenceEquals(_subscribedEffect, eff))
            return;

        if (_subscribedEffect != null)
            _subscribedEffect.Changed -= OnEffectChanged;

        _subscribedEffect = eff;

        if (_subscribedEffect != null)
            _subscribedEffect.Changed += OnEffectChanged;
    }

    private void OnEffectChanged()
    {
        // Runs in editor when you edit fields inside the Effect resource (name/desc/icon/isBuff)
        ApplyEffect();
    }

    // Apply fields derived from the resource
    private void ApplyEffect()
    {
        var eff = EffectResource;

        if (eff != null)
        {
            // Resource drives buff flag
            _buffEffect = eff.IsBuff;
        }

        if (_uiReady)
        {
            ApplyText();
            ApplyTheme();
        }
    }

    private void ApplyTheme()
    {
        if (!IsInsideTree())
            return;

        if (_effectLabelBackground == null)
            _effectLabelBackground = GetNodeOrNull<PanelContainer>("%EffectLabelBackground");

        if (buff_effect)
        {
            if (buff_header != null)
                Theme = buff_header;

            if (buff_body != null && _effectLabelBackground != null)
                _effectLabelBackground.Theme = buff_body;
        }
        else
        {
            Theme = _defaultHeaderTheme;

            if (_effectLabelBackground != null)
                _effectLabelBackground.Theme = _defaultBodyTheme;
        }
    }

    private void ApplyText()
    {
        if (!_uiReady)
            return;

        var eff = EffectResource;

        var icon = eff?.Icon;
        var name = eff?.EffectName ?? "";
        var desc = eff?.EffectDescription ?? "";

        if (_effectIcon != null) _effectIcon.Texture = icon;
        if (_effectNameLabel != null) _effectNameLabel.Text = name;
        if (_effectDescLabel != null) _effectDescLabel.Text = desc;
        if (_effectStatsLabel != null) _effectStatsLabel.Text = _effectVariables;
    }

    private void _on_effect_info_texture_button_pressed()
    {
        if (!infoToggle) // info is hidden
        {
            infoToggle = true;

            if (_effectDescriptionContainer != null)
                _effectDescriptionContainer.Visible = true;

            if (_effectInfoTextureButton != null)
                _effectInfoTextureButton.TextureHover = infoHoverXTexture;
        }
        else
        {    // info is shown
            infoToggle = false;

            if (_effectDescriptionContainer != null)
                _effectDescriptionContainer.Visible = false;

            if (_effectInfoTextureButton != null)
                _effectInfoTextureButton.TextureHover = infoHoverTexture;
        }
    }

    private void _on_hide_timer_timeout()
    {
        if (_effectDescriptionContainer != null)
            _effectDescriptionContainer.Visible = false;
    }
}
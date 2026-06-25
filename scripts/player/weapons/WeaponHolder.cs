using Godot;

public partial class WeaponHolder : Node3D
{
    private enum EquipState
    {
        Empty = 0,
        Idle = 1,
        Drawing = 2,
        Holstering = 3
    }

    private Node3D? _currentModel;
    private ItemInstance? _currentItem;

    // This is the item the hotbar currently wants equipped.
    // It may be different from _currentItem while switching.
    private ItemInstance? _desiredItem;

    private AnimationPlayer? _anim;
    private Transform3D _weaponSocketRestTransform;

    private EquipState _state = EquipState.Empty;

    [Export] public Node3D? WeaponSocket;

    public ItemInstance? CurrentItem => _currentItem;
    public ItemInstance? DesiredItem => _desiredItem;

    public bool IsSwitching =>
        _state == EquipState.Drawing ||
        _state == EquipState.Holstering;

    public override void _Ready()
    {
        _anim = GetNodeOrNull<AnimationPlayer>("%AnimationPlayer");

        if (_anim == null)
        {
            GD.PushError("WeaponHolder: AnimationPlayer not found.");
            return;
        }

        _anim.AnimationFinished += OnAnimationFinished;

        if (WeaponSocket == null)
        {
            GD.Print("WeaponHolder: WeaponSocket not assigned, using self.");
            WeaponSocket = this;
        }

        _weaponSocketRestTransform = WeaponSocket.Transform;
    }

    public bool IsReadyForItem(ItemInstance? item)
    {
        return _state == EquipState.Idle &&
               _currentItem == item &&
               _desiredItem == item &&
               _currentModel != null &&
               IsInstanceValid(_currentModel);
    }

    public void EquipItem(ItemInstance? item)
    {
        GD.Print("WeaponHolder: EquipItem called.");

        if (item == null)
        {
            ClearItem(true);
            return;
        }

        if (item.Definition == null)
        {
            GD.PushError("WeaponHolder: item.Definition is NULL.");
            return;
        }

        GD.Print($"WeaponHolder: desired item is now {item.Definition.ItemName}");

        _desiredItem = item;

        // Already holding the correct item and not switching.
        if (_currentItem == _desiredItem &&
            _currentModel != null &&
            IsInstanceValid(_currentModel) &&
            _state == EquipState.Idle)
        {
            return;
        }

        // If we are holstering, do NOT restart holster every time the player scrolls.
        // Just update _desiredItem and let the current holster finish.
        if (_state == EquipState.Holstering)
            return;

        // If the desired item is the same as the currently visible item while drawing,
        // let the draw continue.
        if (_state == EquipState.Drawing && _currentItem == _desiredItem)
            return;

        // If we are drawing the wrong item because the player switched again,
        // interrupt and holster/remove it.
        if (_state == EquipState.Drawing && _currentItem != _desiredItem)
        {
            StartHolster();
            return;
        }

        // If there is a current model, holster it before spawning the desired item.
        if (_currentModel != null && IsInstanceValid(_currentModel))
        {
            StartHolster();
            return;
        }

        // No model currently visible, so spawn desired immediately.
        SpawnDesiredWeapon();
    }

    public void ClearItem(bool playAnimation = true)
    {
        GD.Print("WeaponHolder: ClearItem called.");

        _desiredItem = null;

        if (_currentModel != null && IsInstanceValid(_currentModel))
        {
            if (playAnimation)
            {
                StartHolster();
                return;
            }

            RemoveCurrentWeaponInstantly();
        }

        _currentItem = null;
        _state = EquipState.Empty;

        StopAnimationAndResetSocket();
    }

    private void StartHolster()
    {
        if (_currentModel == null || !IsInstanceValid(_currentModel))
        {
            RemoveCurrentWeaponInstantly();
            _currentItem = null;
            _state = EquipState.Empty;

            if (_desiredItem != null)
                SpawnDesiredWeapon();

            return;
        }

        // Prevent fast switching from constantly restarting holster.
        if (_state == EquipState.Holstering)
            return;

        _state = EquipState.Holstering;
        PlayAnimationImmediately("holster");
    }

    private void SpawnDesiredWeapon()
    {
        if (_desiredItem == null)
        {
            _currentItem = null;
            _state = EquipState.Empty;
            StopAnimationAndResetSocket();
            return;
        }

        if (WeaponSocket == null)
        {
            GD.PushError("WeaponHolder: WeaponSocket is NULL.");
            return;
        }

        StopAnimationAndResetSocket();

        ItemInstance item = _desiredItem;

        if (item.Definition == null)
        {
            GD.PushError("WeaponHolder: desired item has NULL Definition.");
            _currentItem = null;
            _state = EquipState.Empty;
            return;
        }

        Item def = item.Definition;
        PackedScene? scene = def.WorldModel;

        if (scene == null)
        {
            GD.Print($"WeaponHolder: {def.ItemName} has NULL WorldModel.");
            _currentItem = null;
            _state = EquipState.Empty;
            return;
        }

        GD.Print($"WeaponHolder: loading scene {scene.ResourcePath}");

        Node instance = scene.Instantiate();

        if (instance == null)
        {
            GD.PushError("WeaponHolder: scene.Instantiate() returned NULL.");
            _currentItem = null;
            _state = EquipState.Empty;
            return;
        }

        if (instance is not Node3D modelRoot)
        {
            GD.PushError("WeaponHolder: weapon scene root must be Node3D.");
            instance.QueueFree();
            _currentItem = null;
            _state = EquipState.Empty;
            return;
        }

        modelRoot.Visible = false;

        WeaponSocket.AddChild(modelRoot);

        modelRoot.Position = def.ModelPosition;
        modelRoot.RotationDegrees = def.ModelRotation;
        modelRoot.Scale = def.ModelScale;

        SetVisualLayersRecursive(modelRoot, 2);

        _currentModel = modelRoot;
        _currentItem = item;

        GD.Print($"WeaponHolder: weapon equipped visually: {def.ItemName}");

        _state = EquipState.Drawing;

        PlayAnimationImmediately("draw");

        modelRoot.Visible = true;
    }

    private void OnAnimationFinished(StringName animName)
    {
        string finishedAnimationName = animName.ToString();

        GD.Print($"WeaponHolder: animation finished {finishedAnimationName}");

        if (finishedAnimationName == "holster")
        {
            RemoveCurrentWeaponInstantly();
            _currentItem = null;

            StopAnimationAndResetSocket();

            if (_desiredItem != null)
            {
                SpawnDesiredWeapon();
                return;
            }

            _state = EquipState.Empty;
            return;
        }

        if (finishedAnimationName == "draw")
        {
            // If the player switched again during draw, immediately holster this now-wrong model.
            if (_currentItem != _desiredItem)
            {
                StartHolster();
                return;
            }

            _state = EquipState.Idle;
        }
    }

    private void RemoveCurrentWeaponInstantly()
    {
        if (_currentModel != null && IsInstanceValid(_currentModel))
        {
            Node? parent = _currentModel.GetParent();

            if (parent != null)
                parent.RemoveChild(_currentModel);

            _currentModel.QueueFree();
        }

        _currentModel = null;
    }

    private void StopAnimationAndResetSocket()
    {
        if (_anim != null)
            _anim.Stop();

        ResetSocketToRestTransform();
    }

    private void ResetSocketToRestTransform()
    {
        if (WeaponSocket == null)
            return;

        WeaponSocket.Transform = _weaponSocketRestTransform;
    }

    private void PlayAnimationImmediately(string animationName)
    {
        if (_anim == null)
            return;

        if (!_anim.HasAnimation(animationName))
        {
            GD.PushWarning($"WeaponHolder: Animation '{animationName}' does not exist.");
            return;
        }

        _anim.Stop();
        _anim.Play(animationName);
        _anim.Advance(0.0);
    }

    private void SetVisualLayersRecursive(Node node, uint layerMask)
    {
        if (node is VisualInstance3D visual)
            visual.Layers = layerMask;

        foreach (Node child in node.GetChildren())
            SetVisualLayersRecursive(child, layerMask);
    }
}
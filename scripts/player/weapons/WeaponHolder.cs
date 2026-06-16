using Godot;

public partial class WeaponHolder : Node3D
{
    private Node3D _currentModel;   // currently equipped weapon scene
    private ItemInstance _currentItem;      // item resource that spawned the weapon

    private ItemInstance _pendingItem;      // weapon waiting to be equipped

    private AnimationPlayer _anim;

    [Export] public Node3D WeaponSocket; // where the weapon model attaches (recommended)

    public override void _Ready()
    {
        _anim = GetNode<AnimationPlayer>("%AnimationPlayer");
        _anim.AnimationFinished += OnAnimationFinished;

        // If a socket is not assigned in the inspector,
        // fall back to using this node itself.
        if (WeaponSocket == null)
        {
            GD.Print("WeaponHolder: WeaponSocket not assigned, using self");
            WeaponSocket = this;
        }
    }

    public void EquipItem(ItemInstance item)
    {
        GD.Print("WeaponHolder: EquipItem called");

        // If null item → unequip
        if (item == null)
        {
            GD.Print("WeaponHolder: item is NULL");
            ClearItem();
            return;
        }

        GD.Print($"WeaponHolder: equipping {item.Definition.ItemName}");

        // If the same instance is already equipped → replay draw animation
        if (_currentItem == item && IsInstanceValid(_currentModel))
        {
            GD.Print("WeaponHolder: same item instance, replay draw animation");

            _anim.Play("draw");
            return;
        }

        // Store the item we want to equip
        _pendingItem = item;

        // If a weapon exists → play holster first
        if (_currentModel != null)
        {
            _anim.Play("holster");
            return;
        }

        // Otherwise spawn immediately
        SpawnPendingWeapon();
    }

    public void ClearItem(bool playAnimation = true)
    {
        GD.Print("WeaponHolder: ClearItem called");

        if (_currentModel != null)
        {
            if (playAnimation)
            {
                _anim.Play("holster");
                _pendingItem = null;
                return;
            }

            // instant removal (used when throwing)
            if (IsInstanceValid(_currentModel))
                _currentModel.QueueFree();

            _currentModel = null;
        }

        _currentItem = null;
    }

    private void SpawnPendingWeapon()
    {
        if (_pendingItem == null)
            return;

        ItemInstance item = _pendingItem;
        _pendingItem = null;

        _currentItem = item;

        // Item definition contains the static data
        Item def = item.Definition;

        PackedScene scene = def.WorldModel;

        if (scene == null)
        {
            GD.Print($"WeaponHolder: {def.ItemName} has NULL WorldModel");
            return;
        }

        GD.Print($"WeaponHolder: loading scene {scene.ResourcePath}");

        Node instance = scene.Instantiate();

        if (instance == null)
        {
            GD.PushError("WeaponHolder: scene.Instantiate() returned NULL");
            return;
        }

        Node3D modelRoot = instance as Node3D;

        if (modelRoot == null)
        {
            GD.PushError("WeaponHolder: weapon scene root must be Node3D");
            instance.QueueFree();
            return;
        }

        // Attach weapon to the socket
        WeaponSocket.AddChild(modelRoot);

        // Classic FPS placement (adjust per weapon later if needed)
        modelRoot.Position = def.ModelPosition;
        modelRoot.RotationDegrees = def.ModelRotation;

        // Force weapon visuals onto layer 2 so a dedicated camera can render it
        SetVisualLayersRecursive(modelRoot, 2);

        _currentModel = modelRoot;

        GD.Print("WeaponHolder: weapon equipped successfully");

        PrintTreeRecursive(_currentModel, 0);

        // Play draw animation
        _anim.Play("draw");
    }

    private void OnAnimationFinished(StringName animName)
    {
        GD.Print($"WeaponHolder: animation finished {animName}");

        if (animName == "holster")
        {
            // Remove current weapon
            if (_currentModel != null)
            {
                if (IsInstanceValid(_currentModel))
                    _currentModel.QueueFree();

                _currentModel = null;
            }

            // If we were switching weapons → spawn the next one
            if (_pendingItem != null)
                SpawnPendingWeapon();
        }
    }

    private void SetVisualLayersRecursive(Node node, uint layerMask)
    {
        // Any renderable node (MeshInstance3D, etc.)
        if (node is VisualInstance3D visual)
        {
            visual.Layers = layerMask;
            GD.Print($"WeaponHolder: set layer on {node.Name} -> {layerMask}");
        }

        foreach (Node child in node.GetChildren())
            SetVisualLayersRecursive(child, layerMask);
    }

    private void PrintTreeRecursive(Node node, int depth)
    {
        // Debug helper to print the full structure of the spawned weapon
        string indent = "";

        for (int i = 0; i < depth; i++)
            indent += "  ";

        GD.Print($"{indent}- {node.Name} [{node.GetType().Name}]");

        foreach (Node child in node.GetChildren())
            PrintTreeRecursive(child, depth + 1);
    }
}
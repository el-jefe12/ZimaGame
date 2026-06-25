using Godot;
using System.Collections.Generic;

[Tool]
public partial class WorldItem : RigidBody3D, IInteractable
{
    [Export] public float KillY = -50f;
    [Export] public float RecoverHeight = 0.5f;

    [Export(PropertyHint.ResourceType, "Item")]
    public Resource ItemDefinitionRaw
    {
        get => _itemDefinitionRaw;
        set
        {
            _itemDefinitionRaw = value;

            // In editor/manual placement, changing the raw item creates a fresh instance.
            SyncItemInstanceFromRaw();

            if (!Engine.IsEditorHint() && IsInsideTree())
                QueueBuildModel();
        }
    }

    [Export] public Area3D ItemDetectArea3D;

    private float _checkTimer = 0f;
    private Vector3 _spawnPosition;

    private ItemInstance _itemInstance;
    private Resource _itemDefinitionRaw;
    private Resource _lastRaw;

    private Node3D _modelRoot;
    private Node3D _modelInstance;
    private MeshInstance3D _dummyEditorMesh;

    private Vector3 _lastSafePosition;
    private bool _hasSafePosition = false;

    private bool _buildQueued = false;

    private readonly List<CollisionShape3D> _spawnedCollisionShapes = new List<CollisionShape3D>();

    private bool _hasPendingPhysics = false;
    private Vector3 _pendingLinearVelocity = Vector3.Zero;
    private Vector3 _pendingAngularVelocity = Vector3.Zero;

    private bool _playerInRange = false;

    public Item ItemDefinition => _itemDefinitionRaw as Item;

    public ItemInstance ItemInstance
    {
        get => _itemInstance;
        set
        {
            _itemInstance = value;
            _itemDefinitionRaw = value?.Definition;

            if (IsInsideTree())
                QueueBuildModel();
        }
    }

    public Item ItemData => _itemInstance?.Definition;

    public override void _Ready()
    {
        // Prevent physics from running before the model/collision is ready.
        if (!Engine.IsEditorHint())
        {
            Freeze = true;
            Sleeping = true;
            LinearVelocity = Vector3.Zero;
            AngularVelocity = Vector3.Zero;
        }

        ContactMonitor = true;
        MaxContactsReported = 4;

        _dummyEditorMesh = GetNodeOrNull<MeshInstance3D>("DummyEditorMesh");
        _modelRoot = GetNodeOrNull<Node3D>("ModelRoot");

        if (_modelRoot == null)
        {
            _modelRoot = new Node3D();
            _modelRoot.Name = "ModelRoot";
            AddChild(_modelRoot);
        }

        _spawnPosition = GlobalPosition;

        if (!Engine.IsEditorHint() && _dummyEditorMesh != null)
            _dummyEditorMesh.Visible = false;

        if (ItemDetectArea3D != null)
        {
            ItemDetectArea3D.BodyEntered += OnBodyEntered;
            ItemDetectArea3D.BodyExited += OnBodyExited;
        }

        // Only create a new ItemInstance for manually placed world items.
        // Dropped items already receive their existing ItemInstance before/after entering the scene.
        if (_itemInstance == null)
            SyncItemInstanceFromRaw();

        QueueBuildModel();
    }

    public override void _Process(double delta)
    {
        if (!Engine.IsEditorHint())
            return;

        // Detect inspector item-resource changes in editor.
        if (_itemDefinitionRaw != _lastRaw)
        {
            _lastRaw = _itemDefinitionRaw;
            SyncItemInstanceFromRaw();
            BuildModel();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Engine.IsEditorHint())
            return;

        _checkTimer += (float)delta;

        if (LinearVelocity.Length() < 0.35f)
        {
            if (TryGetGroundPosition(GlobalPosition, out Vector3 ground))
            {
                float verticalDistance = Mathf.Abs(GlobalPosition.Y - ground.Y);

                if (verticalDistance <= 2.0f)
                {
                    _lastSafePosition = ground;
                    _hasSafePosition = true;
                }
            }
        }

        if (_checkTimer < 1.0f)
            return;

        _checkTimer = 0f;

        if (GlobalPosition.Y < KillY)
            RecoverToGround();
    }

    public void PrepareDroppedItem(
        ItemInstance itemInstance,
        Vector3 worldPosition,
        Vector3 linearVelocity,
        Vector3 angularVelocity
    )
    {
        if (itemInstance == null || itemInstance.Definition == null)
        {
            GD.PushError("WorldItem: PrepareDroppedItem received null item.");
            return;
        }

        // Freeze while the item is being initialized/rebuilt.
        Freeze = true;
        Sleeping = true;
        LinearVelocity = Vector3.Zero;
        AngularVelocity = Vector3.Zero;

        ItemInstance = itemInstance;

        GlobalPosition = worldPosition;
        _spawnPosition = worldPosition;

        _pendingLinearVelocity = linearVelocity;
        _pendingAngularVelocity = angularVelocity;
        _hasPendingPhysics = true;

        QueueBuildModel();
    }

    private void ActivateDroppedItemPhysics()
    {
        if (Engine.IsEditorHint())
            return;

        Freeze = false;
        Sleeping = false;

        if (_hasPendingPhysics)
        {
            LinearVelocity = _pendingLinearVelocity;
            AngularVelocity = _pendingAngularVelocity;

            _pendingLinearVelocity = Vector3.Zero;
            _pendingAngularVelocity = Vector3.Zero;
            _hasPendingPhysics = false;
        }
    }

    private void RecoverToGround()
    {
        Vector3 safePosition;

        if (_hasSafePosition)
            safePosition = _lastSafePosition;
        else if (TryGetGroundPosition(GlobalPosition, out Vector3 ground))
            safePosition = ground;
        else if (TryGetGroundPosition(_spawnPosition, out Vector3 spawnGround))
            safePosition = spawnGround;
        else
            safePosition = _spawnPosition + new Vector3(0, RecoverHeight, 0);

        Sleeping = true;
        Freeze = true;

        GlobalPosition = safePosition;

        LinearVelocity = Vector3.Zero;
        AngularVelocity = Vector3.Zero;

        Sleeping = false;
        Freeze = false;
    }

    private void SyncItemInstanceFromRaw()
    {
        Item definition = ItemDefinition;

        if (definition == null)
        {
            _itemInstance = null;
            return;
        }

        _itemInstance = new ItemInstance(definition);
    }

    private void QueueBuildModel()
    {
        if (!IsInsideTree())
            return;

        if (_buildQueued)
            return;

        _buildQueued = true;
        CallDeferred(nameof(BuildModel));
    }

    private void BuildModel()
    {
        _buildQueued = false;

        if (!IsInsideTree() || _modelRoot == null)
            return;

        // Remove collision shapes moved from older generated models.
        for (int i = 0; i < _spawnedCollisionShapes.Count; i++)
        {
            CollisionShape3D shape = _spawnedCollisionShapes[i];

            if (GodotObject.IsInstanceValid(shape))
                shape.QueueFree();
        }

        _spawnedCollisionShapes.Clear();

        foreach (Node child in _modelRoot.GetChildren())
            child.QueueFree();

        _modelInstance = null;

        if (_dummyEditorMesh != null)
            _dummyEditorMesh.Visible = true;

        if (ItemData == null || ItemData.WorldModel == null)
        {
            if (!Engine.IsEditorHint())
                CallDeferred(nameof(ActivateDroppedItemPhysics));

            return;
        }

        Node3D modelScene = ItemData.WorldModel.Instantiate<Node3D>();

        if (modelScene == null)
        {
            GD.PushError("WorldItem: Failed to instantiate WorldModel.");

            if (!Engine.IsEditorHint())
                CallDeferred(nameof(ActivateDroppedItemPhysics));

            return;
        }

        // Apply the item-specific world pickup transform.
        // This affects how the item appears when dropped or placed in the world.
        modelScene.Position = ItemData.WorldModelPosition;
        modelScene.RotationDegrees = ItemData.WorldModelRotation;
        modelScene.Scale = ItemData.WorldModelScale;

        _modelRoot.AddChild(modelScene);
        _modelInstance = modelScene;
        
        if (Engine.IsEditorHint())
        {
            if (_dummyEditorMesh != null)
                _dummyEditorMesh.Visible = false;

            return;
        }

        MoveCollisionShapes(modelScene);

        // Wait one frame so moved collision shapes are properly registered.
        CallDeferred(nameof(ActivateDroppedItemPhysics));
    }

    private void MoveCollisionShapes(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            MoveCollisionShapes(child);

            if (child is CollisionShape3D shape)
            {
                Transform3D globalTransform = shape.GlobalTransform;

                shape.Reparent(this);
                shape.GlobalTransform = globalTransform;

                _spawnedCollisionShapes.Add(shape);
            }
        }
    }

    private void OnBodyEntered(Node body)
    {
        if (body.IsInGroup("player"))
            _playerInRange = true;
    }

    private void OnBodyExited(Node body)
    {
        if (body.IsInGroup("player"))
            _playerInRange = false;
    }

    public void Interact(Node player)
    {
        if (!_playerInRange)
            return;

        Pickup(player);
    }

    private void Pickup(Node player)
    {
        if (_itemInstance == null || _itemInstance.Definition == null)
            return;

        PlayerInventory inventory = player.GetNodeOrNull<PlayerInventory>("Inventory");

        if (inventory == null)
            inventory = player.GetNodeOrNull<PlayerInventory>("%Inventory");

        if (inventory == null)
        {
            GD.PushError("WorldItem: Inventory node not found on player.");
            return;
        }

        // Try hotbar first.
        if (inventory.AddItemToHotbar(_itemInstance))
        {
            QueueFree();
            return;
        }

        // Fallback to normal inventory.
        inventory.AddItemInstance(_itemInstance);

        QueueFree();
    }

    public string GetInteractionText()
    {
        if (ItemData == null)
            return "Pick up";

        return $"Pick up {ItemData.ItemName}";
    }

    public bool CanInteract(Node player)
    {
        return _playerInRange;
    }

    private bool TryGetGroundPosition(Vector3 origin, out Vector3 groundPosition)
    {
        groundPosition = Vector3.Zero;

        PhysicsDirectSpaceState3D spaceState = GetWorld3D().DirectSpaceState;

        Vector3 rayStart = origin + new Vector3(0, 5, 0);
        Vector3 rayEnd = origin + new Vector3(0, -100, 0);

        PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(rayStart, rayEnd);
        query.CollideWithBodies = true;
        query.CollideWithAreas = false;
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };

        Godot.Collections.Dictionary result = spaceState.IntersectRay(query);

        if (result.Count == 0)
            return false;

        Vector3 hit = (Vector3)result["position"];
        groundPosition = hit + new Vector3(0, RecoverHeight, 0);

        return true;
    }
}
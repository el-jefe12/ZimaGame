using Godot;

public partial class WorldItemDropper : Node
{
    [ExportCategory("World Item Scene")]
    [Export] public PackedScene WorldItemScene;

    [ExportCategory("Drop Item")]
    [Export(PropertyHint.ResourceType, "Item")]
    public Resource DropItemDefinitionRaw;

    [ExportCategory("Drop Amount")]
    [Export] public int MinDropCount = 2;
    [Export] public int MaxDropCount = 5;

    [ExportCategory("Drop Position")]
    [Export] public float DropRadius = 0.8f;
    [Export] public float DropHeight = 0.7f;

    [ExportCategory("Drop Physics")]
    [Export] public float DropImpulseStrength = 1.5f;

    [ExportCategory("Single Dragged Item Drop")]
    [Export] public string DropperGroupName = "world_item_dropper";
    [Export] public float DragDropDistance = 1.2f;
    [Export] public float DragDropUpOffset = 0.4f;
    [Export] public float DragDropForwardImpulse = 3.0f;
    [Export] public float DragDropUpImpulse = 1.5f;

    private readonly RandomNumberGenerator _random = new RandomNumberGenerator();

    public override void _Ready()
    {
        _random.Randomize();

        // Lets ItemDropUtility find this dropper from any UI script.
        AddToGroup(DropperGroupName);
    }

    // Used by trees/resources/etc.
    // Spawns random count of NEW item instances from DropItemDefinitionRaw.
    public void DropAt(Vector3 worldPosition)
    {
        Item dropItemDefinition = DropItemDefinitionRaw as Item;

        if (dropItemDefinition == null)
        {
            GD.PrintErr("WorldItemDropper: DropItemDefinitionRaw is not assigned or is not an Item resource.");
            return;
        }

        if (WorldItemScene == null)
        {
            GD.PrintErr("WorldItemDropper: WorldItemScene is not assigned.");
            return;
        }

        int minCount = Mathf.Min(MinDropCount, MaxDropCount);
        int maxCount = Mathf.Max(MinDropCount, MaxDropCount);
        int dropCount = _random.RandiRange(minCount, maxCount);

        Node parent = GetDropParent();

        if (parent == null)
        {
            GD.PrintErr("WorldItemDropper: Could not find parent for dropped items.");
            return;
        }

        for (int i = 0; i < dropCount; i++)
        {
            ItemInstance itemInstance = new ItemInstance(dropItemDefinition);
            WorldItem worldItem = CreateWorldItem();

            if (worldItem == null)
                continue;

            parent.AddChild(worldItem);

            float angle = _random.RandfRange(0.0f, Mathf.Pi * 2.0f);
            float distance = _random.RandfRange(0.0f, DropRadius);

            Vector3 offset = new Vector3(
                Mathf.Cos(angle) * distance,
                DropHeight,
                Mathf.Sin(angle) * distance
            );

            Vector3 dropPosition = worldPosition + offset;

            Vector3 linearVelocity = new Vector3(
                Mathf.Cos(angle),
                0.8f,
                Mathf.Sin(angle)
            ).Normalized() * DropImpulseStrength;

            Vector3 angularVelocity = new Vector3(
                _random.RandfRange(-2.0f, 2.0f),
                _random.RandfRange(-2.0f, 2.0f),
                _random.RandfRange(-2.0f, 2.0f)
            );

            // Important:
            // WorldItem stays frozen until its model/collision is built.
            worldItem.PrepareDroppedItem(
                itemInstance,
                dropPosition,
                linearVelocity,
                angularVelocity
            );
        }

        GD.Print($"WorldItemDropper: Spawned {dropCount} world item drops.");
    }

    // Used by trees/resources/etc.
    public void DropAt(Node3D sourceNode)
    {
        if (sourceNode == null)
        {
            GD.PrintErr("WorldItemDropper: Source node is null.");
            return;
        }

        DropAt(sourceNode.GlobalPosition);
    }

    // Used by inventory/container/hotbar dragging.
    // Drops the EXISTING item instance, preserving durability/container contents/etc.
    public bool DropItemInstanceFromCamera(ItemInstance itemInstance)
    {
        if (itemInstance == null || itemInstance.Definition == null)
        {
            GD.PrintErr("WorldItemDropper: Cannot drop null ItemInstance.");
            return false;
        }

        if (WorldItemScene == null)
        {
            GD.PrintErr("WorldItemDropper: WorldItemScene is not assigned.");
            return false;
        }

        Camera3D camera = GetViewport().GetCamera3D();

        if (camera == null)
        {
            GD.PrintErr("WorldItemDropper: Could not find current Camera3D.");
            return false;
        }

        Node parent = GetDropParent();

        if (parent == null)
        {
            GD.PrintErr("WorldItemDropper: Could not find parent for dragged item drop.");
            return false;
        }

        WorldItem worldItem = CreateWorldItem();

        if (worldItem == null)
            return false;

        parent.AddChild(worldItem);

        Vector3 forward = -camera.GlobalTransform.Basis.Z;

        Vector3 dropPosition =
            camera.GlobalPosition
            + forward * DragDropDistance
            + Vector3.Up * DragDropUpOffset;

        Vector3 linearVelocity =
            forward * DragDropForwardImpulse
            + Vector3.Up * DragDropUpImpulse;

        Vector3 angularVelocity = new Vector3(
            _random.RandfRange(-2.0f, 2.0f),
            _random.RandfRange(-2.0f, 2.0f),
            _random.RandfRange(-2.0f, 2.0f)
        );

        // Important:
        // This uses the SAME ItemInstance that came from inventory/container/hotbar.
        worldItem.PrepareDroppedItem(
            itemInstance,
            dropPosition,
            linearVelocity,
            angularVelocity
        );

        GD.Print($"WorldItemDropper: Dropped dragged item instance: {itemInstance.Definition.ItemName}");

        return true;
    }

    private Node GetDropParent()
    {
        Node parent = GetTree().CurrentScene;

        if (parent != null)
            return parent;

        return GetParent();
    }

    private WorldItem CreateWorldItem()
    {
        if (WorldItemScene == null)
            return null;

        WorldItem worldItem = WorldItemScene.Instantiate<WorldItem>();

        if (worldItem == null)
        {
            GD.PrintErr("WorldItemDropper: WorldItemScene root must have WorldItem.cs attached.");
            return null;
        }

        return worldItem;
    }
}
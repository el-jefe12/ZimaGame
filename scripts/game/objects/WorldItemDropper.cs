using Godot;

public partial class WorldItemDropper : Node
{
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
    [Export] public float ItemMass = 0.4f;

    [ExportCategory("Generated World Item")]
    [Export] public float PickupAreaRadius = 1.2f;

    private readonly RandomNumberGenerator _random = new RandomNumberGenerator();

    public override void _Ready()
    {
        _random.Randomize();
    }

    public void DropAt(Vector3 worldPosition)
    {
        Item dropItemDefinition = DropItemDefinitionRaw as Item;

        if (dropItemDefinition == null)
        {
            GD.PrintErr("WorldItemDropper: DropItemDefinitionRaw is not assigned or is not an Item resource.");
            return;
        }

        int minCount = Mathf.Min(MinDropCount, MaxDropCount);
        int maxCount = Mathf.Max(MinDropCount, MaxDropCount);
        int dropCount = _random.RandiRange(minCount, maxCount);

        Node parent = GetTree().CurrentScene;

        if (parent == null)
        {
            parent = GetParent();
        }

        for (int i = 0; i < dropCount; i++)
        {
            WorldItem worldItem = CreateWorldItem(dropItemDefinition);

            parent.AddChild(worldItem);

            float angle = _random.RandfRange(0.0f, Mathf.Pi * 2.0f);
            float distance = _random.RandfRange(0.0f, DropRadius);

            Vector3 offset = new Vector3(
                Mathf.Cos(angle) * distance,
                DropHeight,
                Mathf.Sin(angle) * distance
            );

            worldItem.GlobalPosition = worldPosition + offset;

            Vector3 impulseDirection = new Vector3(
                Mathf.Cos(angle),
                0.8f,
                Mathf.Sin(angle)
            ).Normalized();

            worldItem.Sleeping = false;
            worldItem.Freeze = false;
            worldItem.LinearVelocity = impulseDirection * DropImpulseStrength;
        }

        GD.Print($"WorldItemDropper: Spawned {dropCount} world item drops.");
    }

    public void DropAt(Node3D sourceNode)
    {
        if (sourceNode == null)
        {
            GD.PrintErr("WorldItemDropper: Source node is null.");
            return;
        }

        DropAt(sourceNode.GlobalPosition);
    }

    private WorldItem CreateWorldItem(Item itemDefinition)
    {
        WorldItem worldItem = new WorldItem();

        worldItem.Name = $"WorldItem_{itemDefinition.ItemName}";
        worldItem.Mass = ItemMass;

        Area3D pickupArea = new Area3D();
        pickupArea.Name = "ItemDetectArea3D";

        CollisionShape3D pickupAreaShape = new CollisionShape3D();
        pickupAreaShape.Name = "PickupAreaShape3D";

        SphereShape3D sphereShape = new SphereShape3D();
        sphereShape.Radius = PickupAreaRadius;

        pickupAreaShape.Shape = sphereShape;
        pickupArea.AddChild(pickupAreaShape);

        worldItem.AddChild(pickupArea);
        worldItem.ItemDetectArea3D = pickupArea;

        worldItem.ItemInstance = new ItemInstance(itemDefinition);

        return worldItem;
    }
}
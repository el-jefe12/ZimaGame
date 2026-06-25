using Godot;

public partial class ItemSpawner : Node3D
{
    [ExportCategory("Item")]
    [Export(PropertyHint.ResourceType, "Item")]
    public Resource ItemResource;

    [ExportCategory("World Item Scene")]
    [Export] public PackedScene WorldItemScene;

    [ExportCategory("Spawn Physics")]
    [Export] public Vector3 SpawnOffset = new Vector3(0.0f, 0.5f, 0.0f);
    [Export] public Vector3 SpawnImpulse = Vector3.Zero;

    public void SpawnItem()
    {
        Item itemDefinition = ItemResource as Item;

        if (itemDefinition == null)
        {
            GD.PushError("ItemSpawner: ItemResource is not assigned or is not an Item.");
            return;
        }

        if (WorldItemScene == null)
        {
            GD.PushError("ItemSpawner: WorldItemScene is not assigned.");
            return;
        }

        WorldItem worldItem = WorldItemScene.Instantiate<WorldItem>();

        if (worldItem == null)
        {
            GD.PushError("ItemSpawner: WorldItemScene root must have WorldItem.cs.");
            return;
        }

        Node parent = GetTree().CurrentScene;

        if (parent == null)
        {
            worldItem.QueueFree();
            GD.PushError("ItemSpawner: CurrentScene is null.");
            return;
        }

        parent.AddChild(worldItem);

        // Set item data after adding to tree so WorldItem can build its model safely.
        worldItem.ItemInstance = new ItemInstance(itemDefinition);

        worldItem.GlobalPosition = GlobalPosition + SpawnOffset;
        worldItem.GlobalRotation = GlobalRotation;

        worldItem.Sleeping = false;
        worldItem.Freeze = false;

        if (SpawnImpulse != Vector3.Zero)
            worldItem.ApplyImpulse(SpawnImpulse, Vector3.Zero);

        GD.Print($"ItemSpawner: Spawned world item: {itemDefinition.ItemName}");
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (inputEvent.IsActionPressed("ui_accept"))
            SpawnItem();
    }
}
using Godot;

public partial class ItemSpawner : Node3D
{
    [Export(PropertyHint.ResourceType, "Item")]
    public Resource ItemResource;

    public void SpawnItem()
    {
        if (ItemResource == null)
            return;

        var scene = ItemResource.Get("scene").As<PackedScene>();
        if (scene == null)
            return;

        var item = scene.Instantiate<Node3D>();

        item.GlobalTransform = GlobalTransform;

        GetTree().CurrentScene.AddChild(item);
    }

	public override void _Input(InputEvent e)
	{
		if (e.IsActionPressed("ui_accept"))
			SpawnItem();
	}
}
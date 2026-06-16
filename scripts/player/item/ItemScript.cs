using Godot;

[Tool]
public partial class ItemScript : Node3D
{
    private Resource _itemResourceRaw;

    [Export(PropertyHint.ResourceType, "Item")]
    public Resource ItemResourceRaw
    {
        get => _itemResourceRaw;
        set
        {
            _itemResourceRaw = value;

            if (IsInsideTree())
                CallDeferred(nameof(ApplyItem));
        }
    }

    public override void _Ready()
    {
        ApplyItem();
    }

    public void ApplyItem()
    {
        if (_itemResourceRaw == null)
            return;

        var scene = _itemResourceRaw.Get("scene").As<PackedScene>();
        if (scene == null)
            return;

        var instance = scene.Instantiate();

        // spawn where this placeholder is
        if (instance is Node3D n)
            n.GlobalTransform = GlobalTransform;

        GetParent().AddChild(instance);

        // assign the item resource to the logic node if present
        var logic = instance.GetNodeOrNull<Node>("Logic");
        if (logic != null)
            logic.Set("ItemResourceRaw", _itemResourceRaw);

        // remove the placeholder
        QueueFree();
    }
}
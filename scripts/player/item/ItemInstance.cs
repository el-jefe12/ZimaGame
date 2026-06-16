using Godot;

public partial class ItemInstance : RefCounted
{
    public Item Definition;

    // Unique runtime identifier
    public int InstanceId;

    // Example runtime properties
    public float Durability = 100f;

    public ItemInstance(Item definition)
    {
        Definition = definition;
        InstanceId = (int)GD.Randi();
    }
}
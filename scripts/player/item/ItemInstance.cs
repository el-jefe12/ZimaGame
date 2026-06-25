using Godot;

public partial class ItemInstance : RefCounted
{
    public Item Definition;

    // Unique runtime identifier.
    public int InstanceId;

    // Example runtime property.
    public float Durability = 100f;

    // Null for normal items.
    // Not null for matchboxes, backpacks, ammo boxes, etc.
    public ContainerInventory ContainerInventory;

    public ItemInstance(Item definition)
    {
        Definition = definition;
        InstanceId = (int)GD.Randi();

        // Only item definitions with ContainerData become containers.
        if (Definition != null && Definition.ContainerData != null)
        {
            ContainerInventory = new ContainerInventory();
            ContainerInventory.InitializeFromData(Definition.ContainerData);
        }
    }

    public bool IsContainer()
    {
        return ContainerInventory != null;
    }
}
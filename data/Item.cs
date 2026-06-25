using Godot;
using System;

[GlobalClass]
public partial class Item : Resource
{
    public enum ItemType
    {
        consumable,
        tool,
        material
    }

    public enum ItemSubType
    {
        thirst,
        hunger,
        health,
        tired,
        tool,
        crafting_material
    }

    [ExportCategory("Basic Info")]
    [Export] public string ItemName = "";

    // Scene containing mesh + collision.
    // This can be reused for world pickup, held model, and UI preview.
    [Export] public PackedScene? WorldModel;

    [ExportCategory("Item Icon")]
    [Export] public Texture2D? Icon;
    [Export] public Vector2I Icon_dimensions = new Vector2I(1, 1);

    [ExportCategory("Held Model Transform")]

    // Transform used when the item is attached to the player's WeaponSocket.
    [Export] public Vector3 ModelPosition = new Vector3(0f, 0f, 0f);
    [Export] public Vector3 ModelRotation = new Vector3(0f, 0f, 0f);
    [Export] public Vector3 ModelScale = new Vector3(1f, 1f, 1f);

    [ExportCategory("World Pickup Transform")]

    // Transform used when the item exists physically in the world as a pickup/drop.
    [Export] public Vector3 WorldModelPosition = new Vector3(0f, 0f, 0f);
    [Export] public Vector3 WorldModelRotation = new Vector3(0f, 0f, 0f);
    [Export] public Vector3 WorldModelScale = new Vector3(1f, 1f, 1f);

    [ExportCategory("Item Scripting")]
    [Export] public Script? ItemScript;

    [Export] public ItemType Item_Type;
    [Export] public ItemSubType Item_SubType;

    [ExportCategory("Item Values")]
    [Export] public float ItemHealth = 100f;
    [Export] public float ItemWeight = 0f;

    // Usually 1 for normal item instances.
    [Export] public float ItemAmount = 1f;

    [ExportCategory("Data")]

    // Optional consumable behaviour.
    [Export] public ItemConsumableData? ConsumableData;

    // Optional container behaviour.
    // If this is null, the item is not a container.
    [Export] public ItemContainerData? ContainerData;

    [ExportCategory("List of Actions")]

    // List of actions the item can perform.
    [Export] public Godot.Collections.Array<ItemAction> Actions = new();
}
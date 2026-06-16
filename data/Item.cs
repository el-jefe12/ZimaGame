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

    //[ExportCategory($"")]
    [Export] public string ItemName;

    // Scene containing mesh + collision
    [Export] public PackedScene WorldModel;

    [ExportCategory("Item Icon")]
    [Export] public Texture2D Icon;
    [Export] Vector2I Icon_dimensions = new Vector2I(1, 1);

    [Export] public Vector3 ModelPosition = new Vector3(0f, 0f, 0f);
    [Export] public Vector3 ModelRotation = new Vector3(0f, 0f, 0f);

    [ExportCategory("Item Scripting")]

    [Export] public Script ItemScript;

    [Export] public ItemType Item_Type; // Specifies the main type of the item (consumable, tool, material)

    [Export] public ItemSubType Item_SubType; // Specifies what kind of item it is in more detail, as in what it affects mainly (hunger, thirst, health, tired), or if it is a tool. 
                                            // crafting material is for now an all encompassing subtype for materials used in crafting, which may have various effects and uses.

    [ExportCategory("Item Values")]

    [Export] public float ItemHealth = 100f;
    [Export] public float ItemWeight = 0f;
    [Export] public float ItemAmount = 1; // always 1

    [ExportCategory("Data")]
    // Optional behaviour data
    [Export] public ItemConsumableData ConsumableData;
    //[Export] public ItemToolData Tool;

    [ExportCategory("List of Actions")]
    // List of actions
    [Export] public Godot.Collections.Array<ItemAction> Actions = new();
}

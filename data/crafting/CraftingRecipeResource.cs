using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class CraftingRecipeResource : Resource
{
    // 3x3 crafting grid.
    // Index layout:
    // 0 1 2
    // 3 4 5
    // 6 7 8

    [Export] public Godot.Collections.Array<Item> Ingredients { get; set; } = new();

    [Export] public Item ResultItem { get; set; }

    [Export] public int ResultAmount { get; set; } = 1;
}
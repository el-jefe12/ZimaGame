using Godot;

[GlobalClass]
public partial class CraftingBookResource : Resource
{
    [Export] public Godot.Collections.Array<CraftingBookPageResource> Pages { get; set; } = new();
}
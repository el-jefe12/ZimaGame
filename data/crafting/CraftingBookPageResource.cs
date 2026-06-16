using Godot;

[GlobalClass]
public partial class CraftingBookPageResource : Resource
{
    [Export] public string DisplayName { get; set; } = "";

    [Export(PropertyHint.MultilineText)]
    public string Description { get; set; } = "";

    [Export] public Texture2D Graphic { get; set; }

    [Export] public CraftingRecipeResource Recipe { get; set; }
}
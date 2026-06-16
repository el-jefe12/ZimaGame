using Godot;
using System;

[Tool]
public partial class TreePainter : Node3D
{
    // Scene to spawn
    [Export] public PackedScene TreeScene;

    // Assign the Terrain3D node here
    [Export] public Node3D TerrainNode;

    // Brush settings
    [Export] public float BrushRadius = 6f;
    [Export] public int Density = 5;

    // Randomization
    [Export] public Vector2 ScaleRange = new Vector2(0.9f, 1.3f);
    [Export] public bool RandomYRotation = true;

    // Terrain filters
    [Export] public float MaxSlope = 35f;
    [Export] public float MinHeight = -100f;
    [Export] public float MaxHeight = 1000f;

    private RandomNumberGenerator _rng = new RandomNumberGenerator();

    // Cached terrain
    private Node3D _terrain;

    public override void _Ready()
    {
        _rng.Randomize();
        _terrain = TerrainNode;
    }

    // Inspector button
    [ExportToolButton("Paint Trees")]
    public Callable PaintTreesButton => Callable.From(PaintTrees);

    /// <summary>
    /// Paint trees around this node
    /// </summary>
    private void PaintTrees()
    {
        if (!Engine.IsEditorHint())
            return;

        if (TreeScene == null)
        {
            GD.Print("TreePainter: TreeScene not assigned.");
            return;
        }

        if (TerrainNode == null)
        {
            GD.Print("TreePainter: TerrainNode not assigned.");
            return;
        }

        _terrain = TerrainNode;

        Vector3 center = GlobalPosition;

        for (int i = 0; i < Density; i++)
        {
            Vector2 offset = RandomPointInCircle();

            Vector3 pos = center + new Vector3(offset.X, 0, offset.Y);

            PaintSingleTree(pos);
        }
    }

    /// <summary>
    /// Random point inside brush
    /// </summary>
    private Vector2 RandomPointInCircle()
    {
        float angle = _rng.RandfRange(0, Mathf.Tau);
        float radius = _rng.RandfRange(0, BrushRadius);

        return new Vector2(
            Mathf.Cos(angle) * radius,
            Mathf.Sin(angle) * radius
        );
    }

    /// <summary>
    /// Spawn one tree aligned to terrain
    /// </summary>
	private void PaintSingleTree(Vector3 position)
	{
		if (TerrainNode == null)
			return;

		// Access Terrain3DData object
		GodotObject terrainData = TerrainNode.Get("data").AsGodotObject();

		float height = (float)terrainData.Call("get_height", position);

		if (float.IsNaN(height))
			return;

		position.Y = height;

		Vector3 normal = (Vector3)terrainData.Call("get_normal", position);

		float slope = Mathf.RadToDeg(Mathf.Acos(normal.Dot(Vector3.Up)));

		if (slope > MaxSlope)
			return;

		Node3D tree = TreeScene.Instantiate<Node3D>();

		AddChild(tree);

		tree.Owner = GetTree().EditedSceneRoot;

		tree.GlobalPosition = position;
	}
}
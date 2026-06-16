using Godot;
using System;

[Tool]
public partial class itemGridLogic : GridContainer
{
    private PackedScene _inventorySlotScene;
    private Vector2I _dimensions = new Vector2I(5, 5);

    [Export(PropertyHint.Range, "1,256,1")]
    public int SlotSize { get; set; } = 40;

    [Export]
    public PackedScene InventorySlotScene
    {
        get => _inventorySlotScene;
        set
        {
            _inventorySlotScene = value;
            TryRebuild();
        }
    }

    [Export]
    public Vector2I Dimensions
    {
        get => _dimensions;
        set
        {
            _dimensions = value;
            TryRebuild();
            InitSlotData();
        }
    }

    public Node[] slot_data = Array.Empty<Node>();

    public override void _Ready()
    {
        TryRebuild();
        InitSlotData();

        // Make sure this Control actually receives gui input
        MouseFilter = MouseFilterEnum.Stop;
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseButton
            && mouseButton.ButtonIndex == MouseButton.Left
            && mouseButton.Pressed)
        {
            // This is already LOCAL to this Control
            Vector2 localMousePos = GetLocalMousePosition();

            (int index, Vector2I cell) = GetSlotFromLocalPos(localMousePos);

            if (index != -1)
            {
                GD.Print($"Clicked slot: index={index}, x={cell.X}, y={cell.Y}");
                GetViewport().SetInputAsHandled();
            }
            else
            {
                GD.Print("Clicked outside grid.");
            }
        }
    }

    // =========================
    // Slot picking (LOCAL pos)
    // =========================

    public (int index, Vector2I cell) GetSlotFromLocalPos(Vector2 localPos)
    {
        int cellX = Mathf.FloorToInt(localPos.X / SlotSize);
        int cellY = Mathf.FloorToInt(localPos.Y / SlotSize);

        // Bounds check against your exported Dimensions
        if (cellX < 0 || cellY < 0 || cellX >= _dimensions.X || cellY >= _dimensions.Y)
            return (-1, new Vector2I(-1, -1));

        int index = (cellY * _dimensions.X) + cellX;
        return (index, new Vector2I(cellX, cellY));
    }

    // If you truly need global input from elsewhere:
    public (int index, Vector2I cell) GetSlotFromGlobalPos(Vector2 globalPos)
    {
        // Convert global -> local using the Control transform
        // (This exists in Godot 4 Control)
        Vector2 localPos = globalPos - GlobalPosition;
        return GetSlotFromLocalPos(localPos);
    }

    public int SlotXYToIndex(Vector2I cell)
    {
        if (cell.X < 0 || cell.Y < 0 || cell.X >= _dimensions.X || cell.Y >= _dimensions.Y)
            return -1;

        return (cell.Y * _dimensions.X) + cell.X;
    }

    public Vector2I IndexToSlotXY(int index)
    {
        if (index < 0)
            return new Vector2I(-1, -1);

        int maxIndex = (_dimensions.X * _dimensions.Y) - 1;
        if (index > maxIndex)
            return new Vector2I(-1, -1);

        int y = index / _dimensions.X;
        int x = index % _dimensions.X;
        return new Vector2I(x, y);
    }

    // =========================
    // Building grid
    // =========================

    private void TryRebuild()
    {
        if (!IsInsideTree())
            return;

        Rebuild();
    }

    public void Rebuild()
    {
        if (_dimensions.X <= 0 || _dimensions.Y <= 0)
        {
            GD.PushWarning($"{Name}: Dimensions must be > 0");
            return;
        }

        if (_inventorySlotScene == null)
        {
            GD.PushWarning($"{Name}: InventorySlotScene is not assigned");
            return;
        }

        Columns = _dimensions.X;

        foreach (Node child in GetChildren())
            child.QueueFree();

        int total = _dimensions.X * _dimensions.Y;
        for (int i = 0; i < total; i++)
        {
            Node slot = _inventorySlotScene.Instantiate<Node>();
            AddChild(slot);

            if (Engine.IsEditorHint())
                slot.Owner = GetTree().EditedSceneRoot;
        }
    }

    public void InitSlotData()
    {
        int total = Mathf.Max(0, _dimensions.X * _dimensions.Y);
        slot_data = new Node[total]; // defaults to null
    }
}

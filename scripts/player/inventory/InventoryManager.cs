using Godot;

public partial class InventoryManager : Node
{
    public static InventoryManager Instance { get; private set; } = null!;

    [Signal] public delegate void InventoryUiEventHandler(bool open, Node? container);

    public bool IsOpen { get; private set; }
    public Node? CurrentContainer { get; private set; }

    public override void _EnterTree() => Instance = this;

    public void TogglePlayerInventory()
    {
        if (IsOpen) Close();
        else OpenPlayerOnly();
    }

    public void OpenPlayerOnly()
    {
        IsOpen = true;
        CurrentContainer = null;
        EmitSignal(SignalName.InventoryUi, true, (Variant)CurrentContainer);
    }

    public void OpenWithContainer(Node container)
    {
        IsOpen = true;
        CurrentContainer = container;
        EmitSignal(SignalName.InventoryUi, true, (Variant)CurrentContainer);
    }

    public void Close()
    {
        IsOpen = false;
        CurrentContainer = null;
        EmitSignal(SignalName.InventoryUi, false, (Variant)CurrentContainer);
    }
}

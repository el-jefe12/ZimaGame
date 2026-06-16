using Godot;
using System;

public partial class SubViewportScreenSizeMatch : SubViewport
{
    public override void _Ready()
    {
        // Set size immediately when the scene starts
        UpdateSize();

        // Listen for window resize events
        GetViewport().SizeChanged += OnMainViewportSizeChanged;
    }

    public override void _ExitTree()
    {
        // Remove event subscription
        if (GetViewport() != null)
            GetViewport().SizeChanged -= OnMainViewportSizeChanged;
    }

    private void OnMainViewportSizeChanged()
    {
        UpdateSize();
    }

    private void UpdateSize()
    {
        // Get the visible game window size
        Vector2I screenSize = (Vector2I)GetViewport().GetVisibleRect().Size;

        // Since this script is attached to the SubViewport,
        // we can assign directly to Size
        Size = screenSize;

        GD.Print($"SubViewport resized to {screenSize.X}x{screenSize.Y}");
    }
}
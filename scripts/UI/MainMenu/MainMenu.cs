using Godot;
using System.Collections.Generic;

public partial class MainMenu : Control
{
    [ExportCategory("Menu Pages")]
    [Export] public Godot.Collections.Array<NodePath> MenuPagePaths { get; set; } = new();

    [Export] public int StartingPageIndex = 0;

    [ExportCategory("Mouse")]
    [Export] public bool ForceMouseVisible = true;

    private readonly List<Control> _menuPages = new();

    public override void _EnterTree()
    {
        MakeMouseUsable();
    }

    public override void _Ready()
    {
        MakeMouseUsable();

        // Runs after other _Ready methods, in case something captures the mouse again.
        CallDeferred(nameof(MakeMouseUsable));

        CachePages();

        if (_menuPages.Count > 0)
        {
            int safeIndex = Mathf.Clamp(StartingPageIndex, 0, _menuPages.Count - 1);
            ShowOnlyPanel(_menuPages[safeIndex]);
        }
    }

    public override void _Process(double delta)
    {
        // Keep this enabled while debugging.
        // If another script captures the mouse, this takes it back.
        if (ForceMouseVisible && Input.MouseMode != Input.MouseModeEnum.Visible)
        {
            MakeMouseUsable();
        }
    }

    public void MakeMouseUsable()
    {
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    private void CachePages()
    {
        _menuPages.Clear();

        foreach (NodePath pagePath in MenuPagePaths)
        {
            Control? page = GetNodeOrNull<Control>(pagePath);

            if (page == null)
            {
                GD.PrintErr($"MainMenu: Could not find menu page at path: {pagePath}");
                continue;
            }

            _menuPages.Add(page);
        }
    }

    public void ShowOnlyPanel(Control panelToShow)
    {
        foreach (Control page in _menuPages)
        {
            page.Visible = page == panelToShow;
        }
    }

    public void HideAllPanels()
    {
        foreach (Control page in _menuPages)
        {
            page.Visible = false;
        }
    }
}
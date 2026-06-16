using Godot;
using System.Threading.Tasks;

public partial class TextScroll : ScrollContainer
{
    [Export] public NodePath RichTextLabelPath = "ItemName";
    [Export] public float Speed = 60f;
    [Export] public float GapPixels = 80f;
    [Export] public float StartDelay = 1.0f;

    private RichTextLabel _label;
    private bool _scrolling;
    private float _timer;

    public override async void _Ready()
    {
        HorizontalScrollMode = ScrollMode.ShowNever;
        VerticalScrollMode = ScrollMode.Disabled;

        _label = GetNode<RichTextLabel>(RichTextLabelPath);
        _label.AutowrapMode = TextServer.AutowrapMode.Off;

        await WaitForRealSize();
        Build();
    }

    public void SetText(string text)
    {
        _label.Text = text;
        CallDeferred(nameof(Build));
    }

    private async void Build()
    {
        _scrolling = false;
        ScrollHorizontal = 0;
        _timer = 0;

        _label.CustomMinimumSize = Vector2.Zero;

        await WaitForRealSize();

        float containerWidth = GetRect().Size.X;
        float textWidth = GetTextWidth();

        // ✅ TEXT FITS → NO SCROLL
        if (textWidth <= containerWidth)
        {
            _label.Text = _label.Text;
            _label.CustomMinimumSize = new Vector2(containerWidth, 0);
            return;
        }

        // duplicate for seamless loop
        int spaces = Mathf.Max(5, Mathf.RoundToInt(GapPixels / 6f));
        string gap = new string(' ', spaces);

        string original = _label.Text;
        _label.Text = original + gap + original;

        await WaitForRealSize();

        float fullWidth = GetTextWidth();

        // 🔒 THIS enables scrolling
        _label.CustomMinimumSize = new Vector2(fullWidth, 0);

        await WaitForRealSize();

        _scrolling = true;
    }

    public override void _Process(double delta)
    {
        if (!_scrolling)
            return;

        int max = GetRealMaxScroll();
        if (max <= 0)
            return;

        _timer += (float)delta;
        if (_timer < StartDelay)
            return;

        ScrollHorizontal = (int)(ScrollHorizontal + Speed * (float)delta);

        if (ScrollHorizontal >= max)
        {
            ScrollHorizontal = 0;
            _timer = 0;
        }
    }

    private int GetRealMaxScroll()
    {
        Control child = (Control)GetChild(0);
        return Mathf.Max(0, (int)(child.CustomMinimumSize.X - GetRect().Size.X));
    }

    private float GetTextWidth()
    {
        if (_label.HasMethod("get_content_width"))
            return (float)(double)_label.Call("get_content_width");

        return _label.GetCombinedMinimumSize().X;
    }

    private async Task WaitForRealSize()
    {
        while (GetRect().Size.X == 0)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}
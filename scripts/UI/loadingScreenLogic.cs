using Godot;

public partial class loadingScreenLogic : Control
{
    [Export(PropertyHint.File, "*.tscn")]
    public string TargetScenePath = "";

    [ExportCategory("Loading UI")]
    [Export] public NodePath LoadingBarPath = "%LoadingScreenLoadingBar";
    [Export] public NodePath LoadingInfoLabelPath = "%LoadingInfoRichTextLabel";

    [ExportCategory("Behavior")]
    [Export] public float SceneLoadMaxPercent = 80.0f;

    private Range? _loadingProgressBar;
    private RichTextLabel? _loadingInfoLabel;

    private bool _requestStarted = false;
    private Node? _loadedWorldInstance;

    public override void _Ready()
    {
        // Menu/loading screens should always have the mouse usable.
        Input.MouseMode = Input.MouseModeEnum.Visible;

        _loadingProgressBar = GetNodeOrNull<Range>(LoadingBarPath);
        _loadingInfoLabel = GetNodeOrNull<RichTextLabel>(LoadingInfoLabelPath);

        if (_loadingProgressBar == null)
        {
            GD.PrintErr("loadingScreenLogic: Loading bar not found, or it is not Range-based.");
            return;
        }

        _loadingProgressBar.MinValue = 0;
        _loadingProgressBar.MaxValue = 100;
        _loadingProgressBar.Value = 0;

        if (string.IsNullOrWhiteSpace(TargetScenePath))
        {
            SetInfo("[color=red]TargetScenePath is empty.[/color]");
            return;
        }

        Error err = ResourceLoader.LoadThreadedRequest(TargetScenePath);

        if (err != Error.Ok)
        {
            SetInfo($"[color=red]Threaded request failed: {err}[/color]");
            return;
        }

        _requestStarted = true;
        SetProgress(0.0f);
        SetInfo("Loading scene...");
    }

    public override void _Process(double delta)
    {
        if (!_requestStarted)
        {
            return;
        }

        Godot.Collections.Array progress = new Godot.Collections.Array();

        ResourceLoader.ThreadLoadStatus status =
            ResourceLoader.LoadThreadedGetStatus(TargetScenePath, progress);

        float ratio = GetProgressRatio(progress);
        float scenePercent = Mathf.Clamp(ratio * SceneLoadMaxPercent, 0.0f, SceneLoadMaxPercent);

        SetProgress(scenePercent);
        SetInfo($"Loading scene... {scenePercent:0}%");

        if (status == ResourceLoader.ThreadLoadStatus.Loaded)
        {
            _requestStarted = false;
            InstantiateLoadedWorld();
        }
        else if (status == ResourceLoader.ThreadLoadStatus.Failed)
        {
            SetInfo("[color=red]Failed to load scene.[/color]");
            _requestStarted = false;
        }
        else if (status == ResourceLoader.ThreadLoadStatus.InvalidResource)
        {
            SetInfo("[color=red]Invalid resource path.[/color]");
            _requestStarted = false;
        }
    }

    private void InstantiateLoadedWorld()
    {
        Resource loadedResource = ResourceLoader.LoadThreadedGet(TargetScenePath);

        PackedScene? packedScene = loadedResource as PackedScene;

        if (packedScene == null)
        {
            SetInfo("[color=red]Loaded resource is not a scene.[/color]");
            return;
        }

        SetProgress(SceneLoadMaxPercent);
        SetInfo("Starting world...");

        _loadedWorldInstance = packedScene.Instantiate();

        if (_loadedWorldInstance == null)
        {
            SetInfo("[color=red]Failed to instantiate loaded scene.[/color]");
            return;
        }

        WorldRoot? worldRoot = _loadedWorldInstance as WorldRoot;

        if (worldRoot == null)
        {
            worldRoot = FindWorldRoot(_loadedWorldInstance);
        }

        if (worldRoot != null)
        {
            worldRoot.WorldLoadingProgress += OnWorldLoadingProgress;
            worldRoot.WorldReady += OnWorldReady;
        }
        else
        {
            GD.PrintErr("loadingScreenLogic: No WorldRoot found in loaded scene. Finishing immediately.");
        }

        SceneTree tree = GetTree();

        // Add the world to the root.
        tree.Root.AddChild(_loadedWorldInstance);

        // Set it as the current scene.
        tree.CurrentScene = _loadedWorldInstance;

        // Keep the loading screen visually on top while the world initializes.
        GetParent()?.RemoveChild(this);
        tree.Root.AddChild(this);

        // If the scene has no WorldRoot, finish after adding it.
        if (worldRoot == null)
        {
            FinishLoading();
        }
    }

    private WorldRoot? FindWorldRoot(Node root)
    {
        if (root is WorldRoot worldRoot)
        {
            return worldRoot;
        }

        foreach (Node child in root.GetChildren())
        {
            WorldRoot? found = FindWorldRoot(child);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private void OnWorldLoadingProgress(float percent, string message)
    {
        float worldPercentRange = 100.0f - SceneLoadMaxPercent;
        float mappedPercent = SceneLoadMaxPercent + Mathf.Clamp(percent, 0.0f, 100.0f) / 100.0f * worldPercentRange;

        SetProgress(mappedPercent);
        SetInfo(message);
    }

    private void OnWorldReady()
    {
        SetProgress(100.0f);
        SetInfo("Done.");
        FinishLoading();
    }

    private void FinishLoading()
    {
        QueueFree();
    }

    private void SetProgress(float percent)
    {
        if (_loadingProgressBar == null)
        {
            return;
        }

        _loadingProgressBar.Value = Mathf.Clamp(percent, 0.0f, 100.0f);
    }

    private void SetInfo(string bbcode)
    {
        if (_loadingInfoLabel == null)
        {
            return;
        }

        _loadingInfoLabel.Text = bbcode;
    }

    private float GetProgressRatio(Godot.Collections.Array progress)
    {
        if (progress.Count <= 0)
        {
            return 0.0f;
        }

        Variant progressVariant = progress[0].As<Variant>();

        if (progressVariant.VariantType == Variant.Type.Float)
        {
            return (float)progressVariant;
        }

        if (progressVariant.VariantType == Variant.Type.Int)
        {
            return (int)progressVariant;
        }

        if (progressVariant.VariantType == Variant.Type.Nil)
        {
            return 0.0f;
        }

        return (float)(double)progressVariant;
    }
}
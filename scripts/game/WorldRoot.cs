using Godot;
using System;
using System.Threading.Tasks;

public partial class WorldRoot : Node3D
{
    [Signal]
    public delegate void WorldLoadingProgressEventHandler(float percent, string message);

    [Signal]
    public delegate void WorldReadyEventHandler();

    [ExportCategory("World References")]
    [Export] public NodePath TerrainPath = new NodePath("");
    [Export] public NodePath TerrainInstanceCollectorPath = new NodePath("");
    [Export] public NodePath DistantModelDetectionPath = new NodePath("");

    private bool _worldReadyEmitted = false;

    public override async void _Ready()
    {
        try
        {
            await InitializeWorldAsync();
        }
        catch (Exception exception)
        {
            GD.PushError($"WorldRoot: InitializeWorldAsync crashed: {exception}");
            FinishWorldLoadingAnyway();
        }
    }

    private async Task InitializeWorldAsync()
    {
        EmitSignal(SignalName.WorldLoadingProgress, 0.0f, "Preparing world...");

        FootstepManager? footstepManager = GetNodeOrNull<FootstepManager>("/root/FootstepManager");

        if (footstepManager == null)
        {
            GD.PushError("WorldRoot: FootstepManager AutoLoad missing at /root/FootstepManager.");
            FinishWorldLoadingAnyway();
            return;
        }

        EmitSignal(SignalName.WorldLoadingProgress, 25.0f, "Checking terrain...");

        if (IsEmptyNodePath(TerrainPath))
        {
            GD.PushError("WorldRoot: TerrainPath is not set.");
            FinishWorldLoadingAnyway();
            return;
        }

        Node? terrain = GetNodeOrNull(TerrainPath);

        if (terrain == null)
        {
            GD.PushError($"WorldRoot: Terrain not found at path: {TerrainPath}.");
            FinishWorldLoadingAnyway();
            return;
        }

        EmitSignal(SignalName.WorldLoadingProgress, 45.0f, "Registering terrain...");

        footstepManager.RegisterTerrain(terrain);

        EmitSignal(SignalName.WorldLoadingProgress, 65.0f, "Collecting terrain instances...");

        TerrainInstanceCollector? collector = await InitializeTerrainInstanceCollectorAsync(terrain);

        if (collector == null)
        {
            GD.PushError("WorldRoot: TerrainInstanceCollector failed. Distant model detection will not initialize.");
            FinishWorldLoadingAnyway();
            return;
        }

        EmitSignal(SignalName.WorldLoadingProgress, 85.0f, "Initializing tree replacements...");

        InitializeDistantModelDetection(terrain, collector);

        FinishWorldLoadingSuccessfully();
    }

    private async Task<TerrainInstanceCollector?> InitializeTerrainInstanceCollectorAsync(Node terrain)
    {
        if (IsEmptyNodePath(TerrainInstanceCollectorPath))
        {
            GD.PrintErr("WorldRoot: TerrainInstanceCollectorPath is not set. Skipping terrain instance collection.");
            return null;
        }

        GD.Print("WorldRoot: calling TerrainInstanceCollector.ParseWhenReadyAsync()");

        TerrainInstanceCollector? collector = GetNodeOrNull<TerrainInstanceCollector>(TerrainInstanceCollectorPath);

        if (collector == null)
        {
            GD.PushError($"WorldRoot: TerrainInstanceCollector not found at path: {TerrainInstanceCollectorPath}.");
            return null;
        }

        if (!GodotObject.IsInstanceValid(collector))
        {
            GD.PushError("WorldRoot: TerrainInstanceCollector exists but is not valid.");
            return null;
        }

        if (terrain == null || !GodotObject.IsInstanceValid(terrain))
        {
            GD.PushError("WorldRoot: Terrain is null or invalid before instance collection.");
            return null;
        }

        Node3D? terrainNode3D = terrain as Node3D;

        if (terrainNode3D == null)
        {
            GD.PushError("WorldRoot: TerrainPath does not point to a Node3D.");
            return null;
        }

        // Force the collector to use the same terrain WorldRoot already found.
        collector.TerrainNode = terrainNode3D;

        bool parsedSuccessfully = false;

        try
        {
            parsedSuccessfully = await collector.ParseWhenReadyAsync();
        }
        catch (Exception exception)
        {
            GD.PushError($"WorldRoot: TerrainInstanceCollector crashed while parsing: {exception}");
            return null;
        }

        if (!GodotObject.IsInstanceValid(collector))
        {
            GD.PushError("WorldRoot: TerrainInstanceCollector became invalid after parsing.");
            return null;
        }

        int collectedTotal = collector.GetTotal();

        if (!parsedSuccessfully)
        {
            GD.PushError($"WorldRoot: TerrainInstanceCollector parsed zero instances. Total = {collectedTotal}.");
            return null;
        }

        GD.Print("WorldRoot: Terrain instances collected = ", collectedTotal);

        return collector;
    }

    private void InitializeDistantModelDetection(Node terrain, TerrainInstanceCollector collector)
    {
        if (IsEmptyNodePath(DistantModelDetectionPath))
        {
            GD.PrintErr("WorldRoot: DistantModelDetectionPath is not set. Skipping distant model detection.");
            return;
        }

        DistantModelDetection? distantModelDetection = GetNodeOrNull<DistantModelDetection>(DistantModelDetectionPath);

        if (distantModelDetection == null)
        {
            GD.PushError($"WorldRoot: DistantModelDetection not found at path: {DistantModelDetectionPath}.");
            return;
        }

        if (!GodotObject.IsInstanceValid(distantModelDetection))
        {
            GD.PushError("WorldRoot: DistantModelDetection exists but is not valid.");
            return;
        }

        distantModelDetection.Collector = collector;
        distantModelDetection.TerrainNode = terrain;

        GD.Print("WorldRoot: initializing DistantModelDetection.");

        distantModelDetection.InitializeAfterTerrainCollected();
    }

    private bool IsEmptyNodePath(NodePath? path)
    {
        if (path == null)
        {
            return true;
        }

        return string.IsNullOrWhiteSpace(path.ToString());
    }

    private void FinishWorldLoadingSuccessfully()
    {
        if (_worldReadyEmitted)
        {
            return;
        }

        _worldReadyEmitted = true;

        EmitSignal(SignalName.WorldLoadingProgress, 100.0f, "World ready.");
        EmitSignal(SignalName.WorldReady);

        GD.Print("[WorldRoot] OK");
    }

    private void FinishWorldLoadingAnyway()
    {
        if (_worldReadyEmitted)
        {
            return;
        }

        _worldReadyEmitted = true;

        EmitSignal(SignalName.WorldLoadingProgress, 100.0f, "World loaded with errors.");
        EmitSignal(SignalName.WorldReady);
    }

    public override void _ExitTree()
    {
        FootstepManager? footstepManager = GetNodeOrNull<FootstepManager>("/root/FootstepManager");

        if (footstepManager != null)
        {
            footstepManager.UnregisterTerrain();
        }
    }
}
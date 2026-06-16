using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class TerrainInstanceCollector : Node
{
    [ExportCategory("Setup")]
    [Export] public Node3D? TerrainNode;

    [ExportCategory("Chunking")]
    [Export] public int ChunkSize = 32;

    [ExportCategory("Timing")]
    [Export] public bool ParseAutomatically = false;
    [Export] public int WaitFramesBeforeParse = 2;

    public Dictionary<Vector2I, List<TerrainInstanceData>> InstancesByChunk
        = new Dictionary<Vector2I, List<TerrainInstanceData>>();

    private readonly Dictionary<Mesh, int> _assetIndexByMesh = new Dictionary<Mesh, int>();
    private readonly Dictionary<string, int> _assetIndexByMeshKey = new Dictionary<string, int>();

    public override async void _Ready()
    {
        if (!ParseAutomatically)
        {
            return;
        }

        await ParseWhenReadyAsync();
    }

    public async Task<bool> ParseWhenReadyAsync()
    {
        SceneTree? tree = GetTree();

        if (tree == null)
        {
            GD.PrintErr("TerrainInstanceCollector: SceneTree is null. Cannot wait before parsing.");
            return false;
        }

        int maxAttempts = Mathf.Max(WaitFramesBeforeParse, 1);

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            await ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            if (!GodotObject.IsInstanceValid(this))
            {
                GD.PrintErr("TerrainInstanceCollector: Collector became invalid while waiting.");
                return false;
            }

            if (TerrainNode == null || !GodotObject.IsInstanceValid(TerrainNode))
            {
                GD.PrintErr("TerrainInstanceCollector: TerrainNode is null/invalid while waiting.");
                return false;
            }

            GD.Print($"TerrainInstanceCollector: parse attempt {attempt}/{maxAttempts}");

            bool parsed = ParseNow();

            if (parsed)
            {
                GD.Print("TerrainInstanceCollector: parse successful.");
                return true;
            }
        }

        GD.PrintErr("TerrainInstanceCollector: failed to parse terrain instances after all attempts.");
        return false;
    }

    public bool ParseNow()
    {
        if (TerrainNode == null)
        {
            GD.PrintErr("TerrainInstanceCollector: TerrainNode not assigned.");
            return false;
        }

        if (!GodotObject.IsInstanceValid(TerrainNode))
        {
            GD.PrintErr("TerrainInstanceCollector: TerrainNode is not valid.");
            return false;
        }

        BuildTerrainAssetLookup();
        ParseTerrain();

        int total = GetTotal();

        GD.Print("TerrainInstanceCollector: instances parsed = ", total);

        return total > 0;
    }

    private void BuildTerrainAssetLookup()
    {
        _assetIndexByMesh.Clear();
        _assetIndexByMeshKey.Clear();

        if (TerrainNode == null || !GodotObject.IsInstanceValid(TerrainNode))
        {
            GD.PrintErr("TerrainInstanceCollector: TerrainNode is null/invalid.");
            return;
        }

        Variant assetsVariant = TerrainNode.Get("assets");

        if (assetsVariant.VariantType == Variant.Type.Nil)
        {
            GD.PrintErr("TerrainInstanceCollector: TerrainNode has no 'assets' property.");
            return;
        }

        GodotObject? assets = assetsVariant.AsGodotObject();

        if (assets == null)
        {
            GD.PrintErr("TerrainInstanceCollector: Terrain assets object is null.");
            return;
        }

        int meshCount = (int)assets.Call("get_mesh_count");

        GD.Print("TerrainInstanceCollector: Terrain mesh asset count = ", meshCount);

        for (int i = 0; i < meshCount; i++)
        {
            GodotObject? meshAsset = assets.Call("get_mesh_asset", i).AsGodotObject();

            if (meshAsset == null)
            {
                GD.PrintErr("TerrainInstanceCollector: asset ", i, " is null.");
                continue;
            }

            Variant meshVariant = meshAsset.Call("get_mesh", 0);
            Mesh? mesh = meshVariant.AsGodotObject() as Mesh;

            if (mesh == null)
            {
                GD.PrintErr("TerrainInstanceCollector: asset ", i, " returned null from get_mesh(0).");
                continue;
            }

            _assetIndexByMesh[mesh] = i;

            string meshKey = BuildMeshKey(mesh);

            if (!_assetIndexByMeshKey.ContainsKey(meshKey))
            {
                _assetIndexByMeshKey[meshKey] = i;
            }

            GD.Print("TerrainInstanceCollector: asset ", i, " mapped | meshKey=", meshKey);
        }
    }

    private void ParseTerrain()
    {
        InstancesByChunk.Clear();

        if (TerrainNode == null || !GodotObject.IsInstanceValid(TerrainNode))
        {
            GD.PrintErr("TerrainInstanceCollector: TerrainNode is null/invalid during ParseTerrain.");
            return;
        }

        List<MultiMeshInstance3D> instancers = new List<MultiMeshInstance3D>();
        CollectInstancers(TerrainNode, instancers);

        GD.Print("TerrainInstanceCollector: instancers found = ", instancers.Count);

        if (instancers.Count <= 0)
        {
            GD.PrintErr("TerrainInstanceCollector: No MultiMeshInstance3D nodes found. Terrain3D may not be ready yet, or TerrainNode points to the wrong node.");
            return;
        }

        Dictionary<int, int> assetCounts = new Dictionary<int, int>();

        foreach (MultiMeshInstance3D instancer in instancers)
        {
            if (!GodotObject.IsInstanceValid(instancer))
            {
                continue;
            }

            MultiMesh? multiMesh = instancer.Multimesh;

            if (multiMesh == null)
            {
                continue;
            }

            Mesh? instancerMesh = multiMesh.Mesh;

            if (instancerMesh == null)
            {
                continue;
            }

            int assetIndex = ResolveAssetIndex(instancerMesh);

            if (assetIndex < 0)
            {
                GD.PrintErr("TerrainInstanceCollector: Could not resolve asset index for instancer: ", instancer.Name);
                continue;
            }

            int count = multiMesh.InstanceCount;

            if (!assetCounts.ContainsKey(assetIndex))
            {
                assetCounts[assetIndex] = 0;
            }

            assetCounts[assetIndex] += count;

            for (int i = 0; i < count; i++)
            {
                Transform3D localTransform = multiMesh.GetInstanceTransform(i);
                Transform3D worldTransform = instancer.GlobalTransform * localTransform;
                Vector3 worldPosition = worldTransform.Origin;

                int chunkX = Mathf.FloorToInt(worldPosition.X / ChunkSize);
                int chunkZ = Mathf.FloorToInt(worldPosition.Z / ChunkSize);

                Vector2I chunk = new Vector2I(chunkX, chunkZ);

                if (!InstancesByChunk.ContainsKey(chunk))
                {
                    InstancesByChunk[chunk] = new List<TerrainInstanceData>();
                }

                TerrainInstanceData instanceData = new TerrainInstanceData
                {
                    AssetIndex = assetIndex,
                    Position = worldPosition,
                    WorldTransform = worldTransform,
                    Instancer = instancer,
                    InstanceIndex = i,
                    OriginalTransform = localTransform
                };

                InstancesByChunk[chunk].Add(instanceData);
            }
        }

        foreach (KeyValuePair<int, int> kv in assetCounts)
        {
            GD.Print("TerrainInstanceCollector: asset ", kv.Key, " totalInstances = ", kv.Value);
        }
    }

    private int ResolveAssetIndex(Mesh mesh)
    {
        if (_assetIndexByMesh.TryGetValue(mesh, out int directIndex))
        {
            return directIndex;
        }

        string meshKey = BuildMeshKey(mesh);

        if (_assetIndexByMeshKey.TryGetValue(meshKey, out int keyedIndex))
        {
            return keyedIndex;
        }

        return -1;
    }

    private string BuildMeshKey(Mesh mesh)
    {
        int surfaceCount = mesh.GetSurfaceCount();
        Aabb aabb = mesh.GetAabb();

        return
            surfaceCount.ToString() + "|" +
            RoundKey(aabb.Position.X) + "|" +
            RoundKey(aabb.Position.Y) + "|" +
            RoundKey(aabb.Position.Z) + "|" +
            RoundKey(aabb.Size.X) + "|" +
            RoundKey(aabb.Size.Y) + "|" +
            RoundKey(aabb.Size.Z);
    }

    private string RoundKey(float value)
    {
        return Mathf.Round(value * 1000.0f).ToString();
    }

    private void CollectInstancers(Node node, List<MultiMeshInstance3D> list)
    {
        if (node == null || !GodotObject.IsInstanceValid(node))
        {
            return;
        }

        foreach (Node child in node.GetChildren())
        {
            if (child is MultiMeshInstance3D multiMeshInstance)
            {
                list.Add(multiMeshInstance);
            }

            CollectInstancers(child, list);
        }
    }

    public int GetTotal()
    {
        int total = 0;

        foreach (KeyValuePair<Vector2I, List<TerrainInstanceData>> kv in InstancesByChunk)
        {
            total += kv.Value.Count;
        }

        return total;
    }
}
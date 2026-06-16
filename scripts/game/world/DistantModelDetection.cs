using Godot;
using System.Collections.Generic;

public partial class DistantModelDetection : Node3D
{
    [ExportCategory("References")]
    [Export] public TerrainInstanceCollector? Collector;
    [Export] public CollisionShape3D? CollisionShapeForRadius;
    [Export] public Node? TerrainNode;

    [ExportCategory("Timing")]
    [Export] public bool InitializeAutomatically = false;
    [Export] public float CheckInterval = 0.25f;

    private float _radiusSq;
    private float _timer;

    private bool _initialized = false;

    private readonly Dictionary<int, PackedScene> _replacementScenes = new Dictionary<int, PackedScene>();
    private readonly Dictionary<long, Node3D> _spawned = new Dictionary<long, Node3D>();

    // If a key is here, the original Terrain3D full tree must stay hidden.
    // But the replacement scene may still spawn nearby as a stump.
    private readonly HashSet<long> _minedInstances = new HashSet<long>();

    private Node3D? _spawnRoot;

    public override void _Ready()
    {
        if (!InitializeAutomatically)
        {
            return;
        }

        InitializeAfterTerrainCollected();
    }

    public void InitializeAfterTerrainCollected()
    {
        if (_initialized)
        {
            return;
        }

        if (Collector == null)
        {
            GD.PrintErr("DistantModelDetection: Collector not assigned.");
            return;
        }

        if (Collector.GetTotal() <= 0)
        {
            GD.PrintErr("DistantModelDetection: Collector has no parsed terrain instances.");
            return;
        }

        if (CollisionShapeForRadius == null)
        {
            GD.PrintErr("DistantModelDetection: CollisionShapeForRadius not assigned.");
            return;
        }

        SphereShape3D? sphere = CollisionShapeForRadius.Shape as SphereShape3D;

        if (sphere == null)
        {
            GD.PrintErr("DistantModelDetection: Collision shape must be SphereShape3D.");
            return;
        }

        if (TerrainNode == null)
        {
            // Fallback: use collector terrain if this was not assigned separately.
            TerrainNode = Collector.TerrainNode;
        }

        if (TerrainNode == null)
        {
            GD.PrintErr("DistantModelDetection: TerrainNode not assigned and Collector.TerrainNode is null.");
            return;
        }

        float radius = sphere.Radius;
        _radiusSq = radius * radius;

        BuildReplacementMapping();

        if (_replacementScenes.Count <= 0)
        {
            GD.PrintErr("DistantModelDetection: No replacement scenes mapped. Check Terrain3D asset metadata: canReplace and replacement_scene.");
            return;
        }

        _spawnRoot = new Node3D();
        _spawnRoot.Name = "DistantModelDetectionSpawnRoot";

        Node parentForSpawnRoot =
            GetTree().CurrentScene != null
                ? GetTree().CurrentScene
                : GetTree().Root;

        parentForSpawnRoot.AddChild(_spawnRoot);

        _initialized = true;

        GD.Print("DistantModelDetection ready.");
        GD.Print("DistantModelDetection: collector instances = ", Collector.GetTotal());
        GD.Print("DistantModelDetection: replacement assets = ", _replacementScenes.Count);
    }

    private void BuildReplacementMapping()
    {
        _replacementScenes.Clear();

        if (TerrainNode == null)
        {
            GD.PrintErr("DistantModelDetection: TerrainNode is null during BuildReplacementMapping.");
            return;
        }

        Variant assetsVariant = TerrainNode.Get("assets");

        if (assetsVariant.VariantType == Variant.Type.Nil)
        {
            GD.PrintErr("DistantModelDetection: TerrainNode has no 'assets' property.");
            return;
        }

        GodotObject? assets = assetsVariant.AsGodotObject();

        if (assets == null)
        {
            GD.PrintErr("DistantModelDetection: Terrain assets object is null.");
            return;
        }

        int meshCount = (int)assets.Call("get_mesh_count");

        GD.Print("DistantModelDetection: Terrain mesh asset count = ", meshCount);

        for (int i = 0; i < meshCount; i++)
        {
            GodotObject? meshAsset = assets.Call("get_mesh_asset", i).AsGodotObject();

            if (meshAsset == null)
            {
                continue;
            }

            bool canReplace = false;

            if (meshAsset.HasMeta("canReplace"))
            {
                canReplace = (bool)meshAsset.GetMeta("canReplace");
            }
            else if (meshAsset.HasMeta("can_replace"))
            {
                canReplace = (bool)meshAsset.GetMeta("can_replace");
            }

            if (!canReplace)
            {
                continue;
            }

            if (!meshAsset.HasMeta("replacement_scene"))
            {
                GD.PrintErr("DistantModelDetection: asset ", i, " missing replacement_scene metadata.");
                continue;
            }

            string path = meshAsset.GetMeta("replacement_scene").AsString();

            if (string.IsNullOrWhiteSpace(path))
            {
                GD.PrintErr("DistantModelDetection: asset ", i, " has empty replacement_scene metadata.");
                continue;
            }

            PackedScene? scene = GD.Load<PackedScene>(path);

            if (scene == null)
            {
                GD.PrintErr("DistantModelDetection: failed to load replacement scene: ", path);
                continue;
            }

            _replacementScenes[i] = scene;

            GD.Print("DistantModelDetection: asset ", i, " replacement -> ", path);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!_initialized)
        {
            return;
        }

        if (Collector == null)
        {
            return;
        }

        if (Collector.InstancesByChunk == null)
        {
            return;
        }

        if (_replacementScenes.Count == 0)
        {
            return;
        }

        if (_spawnRoot == null || !IsInstanceValid(_spawnRoot))
        {
            return;
        }

        _timer += (float)delta;

        if (_timer < CheckInterval)
        {
            return;
        }

        _timer = 0.0f;

        Vector3 center = GlobalPosition;

        int chunkSize = Mathf.Max(Collector.ChunkSize, 1);

        int playerChunkX = Mathf.FloorToInt(center.X / chunkSize);
        int playerChunkZ = Mathf.FloorToInt(center.Z / chunkSize);

        float radius = Mathf.Sqrt(_radiusSq);
        int chunkRadius = Mathf.CeilToInt(radius / chunkSize);

        for (int x = playerChunkX - chunkRadius; x <= playerChunkX + chunkRadius; x++)
        {
            for (int z = playerChunkZ - chunkRadius; z <= playerChunkZ + chunkRadius; z++)
            {
                Vector2I chunk = new Vector2I(x, z);

                if (!Collector.InstancesByChunk.TryGetValue(chunk, out List<TerrainInstanceData>? instances))
                {
                    continue;
                }

                for (int i = 0; i < instances.Count; i++)
                {
                    TerrainInstanceData inst = instances[i];

                    if (inst == null || inst.Instancer == null)
                    {
                        continue;
                    }

                    if (!_replacementScenes.ContainsKey(inst.AssetIndex))
                    {
                        continue;
                    }

                    long key = GetInstanceKey(inst);
                    float distSq = inst.Position.DistanceSquaredTo(center);

                    bool isMined = _minedInstances.Contains(key);
                    bool isSpawned = _spawned.TryGetValue(key, out Node3D? obj);

                    if (isMined)
                    {
                        HideInstance(inst);

                        if (isSpawned)
                        {
                            if (distSq > _radiusSq)
                            {
                                if (obj != null && IsInstanceValid(obj))
                                {
                                    obj.QueueFree();
                                }

                                _spawned.Remove(key);
                            }
                        }
                        else
                        {
                            if (distSq <= _radiusSq)
                            {
                                SpawnReplacement(inst, key, true);
                            }
                        }

                        continue;
                    }

                    if (isSpawned)
                    {
                        if (distSq > _radiusSq)
                        {
                            if (obj != null && IsInstanceValid(obj))
                            {
                                obj.QueueFree();
                            }

                            _spawned.Remove(key);

                            ShowInstance(inst);
                        }
                    }
                    else
                    {
                        if (distSq <= _radiusSq)
                        {
                            SpawnReplacement(inst, key, false);
                        }
                    }
                }
            }
        }
    }

    private void SpawnReplacement(TerrainInstanceData inst, long key, bool spawnAsMined)
    {
        if (_spawnRoot == null)
        {
            return;
        }

        if (!_replacementScenes.TryGetValue(inst.AssetIndex, out PackedScene? scene))
        {
            return;
        }

        Node3D? newObj = scene.Instantiate<Node3D>();

        if (newObj == null)
        {
            return;
        }

        _spawnRoot.AddChild(newObj);
        newObj.GlobalTransform = inst.WorldTransform;

        _spawned[key] = newObj;

        HideInstance(inst);

        if (spawnAsMined)
        {
            ApplyMinedStateToSpawnedObject(newObj);
            GD.Print("DistantModelDetection: Spawned mined stump replacement for key ", key);
            return;
        }

        ConnectDamageableDeath(newObj, inst, key);

        GD.Print("DistantModelDetection: Spawned full replacement for key ", key);
    }

    private void ConnectDamageableDeath(Node3D spawnedObject, TerrainInstanceData inst, long key)
    {
        Damageable? damageable = FindDamageableRecursive(spawnedObject);

        if (damageable == null)
        {
            return;
        }

        damageable.Died += () =>
        {
            MarkInstanceAsMined(inst, key);
        };
    }

    private void MarkInstanceAsMined(TerrainInstanceData inst, long key)
    {
        if (_minedInstances.Contains(key))
        {
            return;
        }

        _minedInstances.Add(key);

        HideInstance(inst);

        GD.Print("DistantModelDetection: Marked terrain instance as mined. Key = ", key);
    }

    private void ApplyMinedStateToSpawnedObject(Node3D spawnedObject)
    {
        DamageableTree? tree = FindDamageableTreeRecursive(spawnedObject);

        if (tree == null)
        {
            GD.PrintErr("DistantModelDetection: Spawned mined object has no DamageableTree, cannot show stump state.");
            return;
        }

        tree.ApplyMinedState();
    }

    private Damageable? FindDamageableRecursive(Node node)
    {
        if (node is Damageable damageable)
        {
            return damageable;
        }

        foreach (Node child in node.GetChildren())
        {
            Damageable? found = FindDamageableRecursive(child);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private DamageableTree? FindDamageableTreeRecursive(Node node)
    {
        if (node is DamageableTree damageableTree)
        {
            return damageableTree;
        }

        foreach (Node child in node.GetChildren())
        {
            DamageableTree? found = FindDamageableTreeRecursive(child);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private long GetInstanceKey(TerrainInstanceData inst)
    {
        return ((long)inst.Instancer.GetInstanceId() << 32) | (uint)inst.InstanceIndex;
    }

    private void HideInstance(TerrainInstanceData inst)
    {
        if (inst == null || inst.Instancer == null)
        {
            return;
        }

        MultiMesh? multiMesh = inst.Instancer.Multimesh;

        if (multiMesh == null)
        {
            return;
        }

        Transform3D transform = inst.OriginalTransform;
        transform.Basis = transform.Basis.Scaled(Vector3.Zero);

        multiMesh.SetInstanceTransform(inst.InstanceIndex, transform);
    }

    private void ShowInstance(TerrainInstanceData inst)
    {
        if (inst == null || inst.Instancer == null)
        {
            return;
        }

        MultiMesh? multiMesh = inst.Instancer.Multimesh;

        if (multiMesh == null)
        {
            return;
        }

        multiMesh.SetInstanceTransform(inst.InstanceIndex, inst.OriginalTransform);
    }
}
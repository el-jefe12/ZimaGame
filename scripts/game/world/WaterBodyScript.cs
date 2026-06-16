using Godot;

[Tool]
public partial class WaterBodyScript : Node3D
{
    [ExportGroup("Water Size")]

    [Export]
    public Vector2 WaterSize
    {
        get => _waterSize;
        set
        {
            _waterSize = new Vector2(
                Mathf.Max(0.1f, value.X),
                Mathf.Max(0.1f, value.Y)
            );

            RequestApplyWaterSettings();
        }
    }

    [Export]
    public float AreaHeight
    {
        get => _areaHeight;
        set
        {
            _areaHeight = Mathf.Max(0.1f, value);
            RequestApplyWaterSettings();
        }
    }

    [Export]
    public float AreaVerticalOffset
    {
        get => _areaVerticalOffset;
        set
        {
            _areaVerticalOffset = value;
            RequestApplyWaterSettings();
        }
    }

    [ExportGroup("Water Gameplay")]

    [Export] public float MaxAllowedDepth = 0.85f;
    [Export] public bool IsFreezing = true;
    [Export] public float FreezingDamagePerSecond = 12.0f;
    [Export] public float MovementSlowMultiplier = 0.82f;

    [ExportGroup("Water Material")]

    [Export] public bool UpdateShaderSizeParameter = true;
    [Export] public string ShaderSizeParameterName = "water_size";

    [ExportGroup("Debug")]

    [Export] public bool PrintDebugMessages = false;

    private Vector2 _waterSize = new Vector2(40.0f, 40.0f);
    private float _areaHeight = 8.0f;
    private float _areaVerticalOffset = -3.0f;

    private MeshInstance3D _waterMesh;
    private Area3D _waterArea;
    private CollisionShape3D _waterAreaShape;
    private Marker3D _surfaceMarker;

    private PlaneMesh _planeMesh;
    private BoxShape3D _areaBoxShape;

    private bool _hasConnectedAreaSignals = false;
    private bool _applyQueued = false;

    public float WaterSurfaceY
    {
        get
        {
            if (_surfaceMarker != null)
                return _surfaceMarker.GlobalPosition.Y;

            return GlobalPosition.Y;
        }
    }

    public override void _EnterTree()
    {
        CacheNodes();
        EnsureUniqueResources();
        ApplyWaterSettings();
    }

    public override void _Ready()
    {
        CacheNodes();
        EnsureUniqueResources();
        ApplyWaterSettings();

        if (!Engine.IsEditorHint())
            ConnectAreaSignals();
    }

    public override void _ExitTree()
    {
        if (!Engine.IsEditorHint())
            DisconnectAreaSignals();
    }

    private void RequestApplyWaterSettings()
    {
        if (!IsInsideTree())
            return;

        if (_applyQueued)
            return;

        _applyQueued = true;

        // Deferred is safer in editor because exported setters can fire while Godot
        // is still constructing or refreshing the scene.
        CallDeferred(nameof(ApplyWaterSettingsDeferred));
    }

    private void ApplyWaterSettingsDeferred()
    {
        _applyQueued = false;
        ApplyWaterSettings();
    }

    private void CacheNodes()
    {
        _waterMesh = GetNodeOrNull<MeshInstance3D>("%WaterVisualMeshInstance3D");
        _waterArea = GetNodeOrNull<Area3D>("%WaterArea3D");
        _waterAreaShape = GetNodeOrNull<CollisionShape3D>("%WaterCollisionShape3D");
        _surfaceMarker = GetNodeOrNull<Marker3D>("%WaterMarker3D");
    }

    private void EnsureUniqueResources()
    {
        EnsureUniquePlaneMesh();
        EnsureUniqueAreaShape();
        EnsureUniqueShaderMaterial();
    }

    private void EnsureUniquePlaneMesh()
    {
        if (_waterMesh == null)
            return;

        if (_waterMesh.Mesh is PlaneMesh existingPlaneMesh)
        {
            // Duplicate so each placed WaterBody instance can have its own mesh size.
            _planeMesh = existingPlaneMesh.Duplicate() as PlaneMesh;
        }
        else
        {
            _planeMesh = new PlaneMesh();
        }

        if (_planeMesh == null)
            _planeMesh = new PlaneMesh();

        _planeMesh.ResourceLocalToScene = true;
        _waterMesh.Mesh = _planeMesh;
    }

    private void EnsureUniqueAreaShape()
    {
        if (_waterAreaShape == null)
            return;

        if (_waterAreaShape.Shape is BoxShape3D existingBoxShape)
        {
            // Duplicate so each placed WaterBody instance can have its own area size.
            _areaBoxShape = existingBoxShape.Duplicate() as BoxShape3D;
        }
        else
        {
            _areaBoxShape = new BoxShape3D();
        }

        if (_areaBoxShape == null)
            _areaBoxShape = new BoxShape3D();

        _areaBoxShape.ResourceLocalToScene = true;
        _waterAreaShape.Shape = _areaBoxShape;
    }

    private void EnsureUniqueShaderMaterial()
    {
        if (_waterMesh == null)
            return;

        Material material = _waterMesh.GetActiveMaterial(0);

        if (material == null)
            return;

        // Important: duplicated material means changing shader params on one water
        // instance will not affect every other water instance using the same material.
        Material uniqueMaterial = material.Duplicate() as Material;

        if (uniqueMaterial == null)
            return;

        uniqueMaterial.ResourceLocalToScene = true;
        _waterMesh.SetSurfaceOverrideMaterial(0, uniqueMaterial);
    }

    private void ApplyWaterSettings()
    {
        if (!IsInsideTree())
            return;

        CacheNodes();

        if (_waterMesh == null)
        {
            PrintDebug("Missing %WaterVisualMeshInstance3D.");
            return;
        }

        if (_waterArea == null)
        {
            PrintDebug("Missing %WaterArea3D.");
            return;
        }

        if (_waterAreaShape == null)
        {
            PrintDebug("Missing %WaterCollisionShape3D.");
            return;
        }

        if (_planeMesh == null || _waterMesh.Mesh != _planeMesh)
        {
            if (_waterMesh.Mesh is PlaneMesh existingPlaneMesh)
                _planeMesh = existingPlaneMesh;
            else
                _planeMesh = new PlaneMesh();

            _planeMesh.ResourceLocalToScene = true;
            _waterMesh.Mesh = _planeMesh;
        }

        if (_areaBoxShape == null || _waterAreaShape.Shape != _areaBoxShape)
        {
            if (_waterAreaShape.Shape is BoxShape3D existingBoxShape)
                _areaBoxShape = existingBoxShape;
            else
                _areaBoxShape = new BoxShape3D();

            _areaBoxShape.ResourceLocalToScene = true;
            _waterAreaShape.Shape = _areaBoxShape;
        }

        ApplyMeshSize();
        ApplyAreaSize();
        ApplySurfaceMarker();
        ApplyShaderSizeParameter();

        NotifyPropertyListChanged();
    }

    private void ApplyMeshSize()
    {
        if (_waterMesh == null || _planeMesh == null)
            return;

        // PlaneMesh.Size uses X/Z dimensions.
        _planeMesh.Size = _waterSize;

        // Water visual sits exactly on the root node height.
        _waterMesh.Position = Vector3.Zero;
        _waterMesh.Scale = Vector3.One;
    }

    private void ApplyAreaSize()
    {
        if (_waterArea == null || _areaBoxShape == null)
            return;

        // BoxShape3D.Size uses X/Y/Z.
        // _waterSize.X = world X size.
        // _waterSize.Y = world Z size.
        _areaBoxShape.Size = new Vector3(
            _waterSize.X,
            _areaHeight,
            _waterSize.Y
        );

        _waterArea.Position = new Vector3(
            0.0f,
            _areaVerticalOffset,
            0.0f
        );

        _waterArea.Scale = Vector3.One;
    }

    private void ApplySurfaceMarker()
    {
        if (_surfaceMarker == null)
            return;

        // Root node Y is the actual water surface height.
        _surfaceMarker.Position = Vector3.Zero;
    }

    private void ApplyShaderSizeParameter()
    {
        if (!UpdateShaderSizeParameter)
            return;

        if (_waterMesh == null)
            return;

        Material material = _waterMesh.GetActiveMaterial(0);

        if (material is not ShaderMaterial shaderMaterial)
            return;

        shaderMaterial.SetShaderParameter(
            ShaderSizeParameterName,
            _waterSize
        );
    }

    private void ConnectAreaSignals()
    {
        if (_waterArea == null)
            return;

        if (_hasConnectedAreaSignals)
            return;

        _waterArea.BodyEntered += OnBodyEntered;
        _waterArea.BodyExited += OnBodyExited;

        _hasConnectedAreaSignals = true;
    }

    private void DisconnectAreaSignals()
    {
        if (_waterArea == null)
            return;

        if (!_hasConnectedAreaSignals)
            return;

        _waterArea.BodyEntered -= OnBodyEntered;
        _waterArea.BodyExited -= OnBodyExited;

        _hasConnectedAreaSignals = false;
    }

    private void OnBodyEntered(Node3D body)
    {
        PlayerWaterHandler waterHandler =
            body.GetNodeOrNull<PlayerWaterHandler>("%PlayerWaterHandler");

        if (waterHandler != null)
            waterHandler.EnterWaterBody(this);
    }

    private void OnBodyExited(Node3D body)
    {
        PlayerWaterHandler waterHandler =
            body.GetNodeOrNull<PlayerWaterHandler>("%PlayerWaterHandler");

        if (waterHandler != null)
            waterHandler.ExitWaterBody(this);
    }

    private void PrintDebug(string message)
    {
        if (!PrintDebugMessages)
            return;

        GD.Print($"WaterBody: {message}");
    }
}
using Godot;

public partial class FireBillboard : MeshInstance3D
{
    [ExportCategory("Billboard")]
    [Export] public bool Enabled = true;

    // If the plane faces the wrong way, change this to 0 or 180.
    [Export] public float YawOffsetDegrees = 180.0f;

    [ExportCategory("Camera Search")]
    [Export] public bool SearchTreeIfViewportCameraMissing = true;

    private Camera3D? _camera;

    private float _initialLocalRotationX = 0.0f;
    private float _initialLocalRotationZ = 0.0f;

    public override void _Ready()
    {
        // Run after most player/camera movement scripts.
        ProcessPriority = 100;

        // Keep the plane's editor-set tilt. The script only changes Y rotation.
        _initialLocalRotationX = Rotation.X;
        _initialLocalRotationZ = Rotation.Z;
    }

    public override void _Process(double delta)
    {
        if (!Enabled)
        {
            return;
        }

        _camera = GetCurrentCamera();

        if (_camera == null)
        {
            return;
        }

        RotateTowardCamera();
    }

    private Camera3D? GetCurrentCamera()
    {
        Camera3D? viewportCamera = GetViewport().GetCamera3D();

        if (viewportCamera != null && IsInstanceValid(viewportCamera))
        {
            return viewportCamera;
        }

        if (!SearchTreeIfViewportCameraMissing)
        {
            return null;
        }

        return FindFirstCamera(GetTree().CurrentScene);
    }

    private Camera3D? FindFirstCamera(Node? node)
    {
        if (node == null)
        {
            return null;
        }

        if (node is Camera3D camera)
        {
            return camera;
        }

        foreach (Node child in node.GetChildren())
        {
            Camera3D? foundCamera = FindFirstCamera(child);

            if (foundCamera != null)
            {
                return foundCamera;
            }
        }

        return null;
    }

    private void RotateTowardCamera()
    {
        Vector3 firePosition = GlobalPosition;
        Vector3 cameraPosition = _camera!.GlobalPosition;

        float directionX = cameraPosition.X - firePosition.X;
        float directionZ = cameraPosition.Z - firePosition.Z;

        float distanceSquared = (directionX * directionX) + (directionZ * directionZ);

        if (distanceSquared < 0.0001f)
        {
            return;
        }

        float worldYawRadians = Mathf.Atan2(directionX, directionZ);
        worldYawRadians += Mathf.DegToRad(YawOffsetDegrees);

        float localYawRadians = worldYawRadians;

        Node? parent = GetParent();

        if (parent is Node3D parentNode)
        {
            localYawRadians -= parentNode.GlobalRotation.Y;
        }

        Rotation = new Vector3(
            _initialLocalRotationX,
            localYawRadians,
            _initialLocalRotationZ
        );
    }
}
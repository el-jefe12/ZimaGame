using Godot;

public partial class WeaponCameraSync : Node
{
    [Export] public Camera3D PlayerCamera;
    [Export] public Camera3D WeaponCamera;

    public override void _Process(double delta)
    {
        if (PlayerCamera == null || WeaponCamera == null)
            return;

        WeaponCamera.GlobalTransform = PlayerCamera.GlobalTransform;
    }
}
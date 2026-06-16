using Godot;

public class TerrainInstanceData
{
    public int AssetIndex = -1;

    public Vector3 Position;

    public Transform3D WorldTransform;

    public MultiMeshInstance3D Instancer;

    public int InstanceIndex;

    public Transform3D OriginalTransform;
}
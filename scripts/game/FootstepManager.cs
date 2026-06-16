using Godot;
using System;

public partial class FootstepManager : Node
{
    // Optional: You can set this in the inspector on the AutoLoad,
    // but for most projects you'll just call RegisterTerrain() from WorldRoot.
    [Export] public NodePath TerrainPath { get; set; } = new NodePath();

    /// <summary>
    /// Blend threshold:
    /// - below this, we treat base as dominant
    /// - above this, we treat overlay as dominant
    /// Terrain3D blending is not pixel-perfect by design.
    /// </summary>
    [Export(PropertyHint.Range, "0,1,0.01")]
    public float OverlayDominantThreshold { get; set; } = 0.5f;

    private GodotObject? _terrainObj;
    private GodotObject? _dataObj;
    private GodotObject? _assetsObj;

    public override void _Ready()
    {

        GD.Print("[FootstepManager] AutoLoad _Ready()");
        GD.Print($"[FootstepManager] TerrainPath: '{TerrainPath}' IsEmpty={TerrainPath.IsEmpty}");
        // AutoLoad starts before the world scene exists.
        // So we don't hard-fail here.
        // If TerrainPath is set AND already exists, we bind now; otherwise WorldRoot should call RegisterTerrain().
        if (!TerrainPath.IsEmpty)
        {
            Node? terrainNode = GetNodeOrNull(TerrainPath);
            if (terrainNode != null)
            {
                RegisterTerrain(terrainNode);
                GD.Print($"[FootstepManager] OK");
            }
            else
            {
                GD.PushWarning(
                    $"{nameof(FootstepManager)}: TerrainPath set but node not found yet. " +
                    "World should call RegisterTerrain() after it loads."
                );
            }
        }
    }

    public bool IsTerrainRegistered()
    {
        if (_dataObj == null || _assetsObj == null)
            return false;

        if (!GodotObject.IsInstanceValid(_dataObj))
            return false;

        if (!GodotObject.IsInstanceValid(_assetsObj))
            return false;

        return true;
    }

    public void UnregisterTerrain()
    {
        _terrainObj = null;
        _dataObj = null;
        _assetsObj = null;
    }

    public void RegisterTerrain(Node terrainNode)
    {
        GodotObject? terrainObj = terrainNode as GodotObject;
        if (terrainObj == null)
        {
            GD.PushError($"{nameof(FootstepManager)}: RegisterTerrain got a node that isn't a GodotObject.");
            UnregisterTerrain();
            return;
        }

        // Terrain3D has properties: data (Terrain3DData) and assets (Terrain3DAssets)
        GodotObject? dataObj = terrainObj.Get("data").AsGodotObject();
        GodotObject? assetsObj = terrainObj.Get("assets").AsGodotObject();

        _terrainObj = terrainObj;
        _dataObj = dataObj;
        _assetsObj = assetsObj;

        GD.Print($"[FootstepManager] assets class = {_assetsObj?.GetClass() ?? "<null>"}");
        GD.Print($"[FootstepManager] has get_texture_asset = {(_assetsObj != null && _assetsObj.HasMethod("get_texture_asset"))}");
        GD.Print($"[FootstepManager] has texture_list prop = {(_assetsObj != null && _assetsObj.Get("texture_list").VariantType != Variant.Type.Nil)}");

        if (_dataObj == null)
            GD.PushError($"{nameof(FootstepManager)}: Terrain3D 'data' is null (terrain not initialized?).");

        if (_assetsObj == null)
            GD.PushError($"{nameof(FootstepManager)}: Terrain3D 'assets' is null (no assets assigned?).");
    }

    /// <summary>
    /// Returns raw (baseId, overlayId, blend).
    /// If outside any Terrain3D region / hole, returns false.
    /// </summary>
    public bool TryGetTextureInfo(Vector3 worldPos, out int baseId, out int overlayId, out float blend)
    {
        baseId = -1;
        overlayId = -1;
        blend = 0.0f;

        if (_dataObj == null || !GodotObject.IsInstanceValid(_dataObj))
            return false;

        Variant result = _dataObj.Call("get_texture_id", worldPos);
        Vector3 info = result.AsVector3();

        // Terrain3D returns (NaN, NaN, NaN) if outside region/hole
        if (float.IsNaN(info.X) || float.IsNaN(info.Y) || float.IsNaN(info.Z))
            return false;

        baseId = (int)info.X;
        overlayId = (int)info.Y;
        blend = info.Z;
        return true;
    }

    /// <summary>
    /// Picks a dominant texture id based on blend threshold.
    /// </summary>
    public bool TryGetDominantTextureId(Vector3 worldPos, out int dominantId)
    {
        dominantId = -1;

        if (!TryGetTextureInfo(worldPos, out int baseId, out int overlayId, out float blend))
            return false;

        dominantId = (blend >= OverlayDominantThreshold) ? overlayId : baseId;
        return true;
    }

    /// <summary>
    /// Returns the Terrain3DTextureAsset.name for the dominant texture at worldPos.
    /// </summary>
    public bool TryGetDominantTextureName(Vector3 worldPos, out string textureName)
    {
        textureName = "";

        if (!TryGetDominantTextureId(worldPos, out int id))
            return false;

        if (_assetsObj == null || !GodotObject.IsInstanceValid(_assetsObj))
            return false;

        // 1) Preferred: use method if it exists
        if (_assetsObj.HasMethod("get_texture_asset"))
        {
            Variant assetVar = _assetsObj.Call("get_texture_asset", id);
            GodotObject? assetObj = assetVar.AsGodotObject();
            if (assetObj == null || !GodotObject.IsInstanceValid(assetObj))
                return false;

            textureName = assetObj.Get("name").AsString();
            return true;
        }

        // 2) Fallback: read from texture_list array
        Variant listVar = _assetsObj.Get("texture_list");
        Godot.Collections.Array list = listVar.AsGodotArray();

        if (list == null || id < 0 || id >= list.Count)
            return false;

        GodotObject? texAssetObj = list[id].AsGodotObject();
        if (texAssetObj == null || !GodotObject.IsInstanceValid(texAssetObj))
            return false;

        textureName = texAssetObj.Get("name").AsString();
        return true;
    }


    /// <summary>
    /// Convenience: gets dominant id + name in one call.
    /// </summary>
    public bool TryGetDominantTexture(Vector3 worldPos, out int id, out string name)
    {
        id = -1;
        name = "";

        if (!TryGetDominantTextureId(worldPos, out id))
            return false;

        return TryGetDominantTextureName(worldPos, out name);
    }

    public void DebugPrintSurfaceAt(Vector3 worldPos)
    {
        if (TryGetDominantTexture(worldPos, out int id, out string name))
            GD.Print($"[FootstepManager] Surface id={id} name={name}");
        else
            GD.Print("[FootstepManager] No Terrain3D surface at that position (not registered or outside terrain).");
    }

}

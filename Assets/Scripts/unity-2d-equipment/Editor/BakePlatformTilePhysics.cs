#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Reimports Tiles.png so custom physics shapes in the meta file apply to tile colliders.
/// Grass islands use a thin top outline; stones use a full box.
/// </summary>
public static class BakePlatformTilePhysics
{
    const string TilesTexturePath = "Assets/Art/Tiles.png";

    [MenuItem("Tools/Platformer/Refresh Tile Colliders")]
    public static void Refresh()
    {
        AssetDatabase.ImportAsset(TilesTexturePath, ImportAssetOptions.ForceUpdate);
        Debug.Log(
            "Tile colliders refreshed. In the scene, select Grid → Tilemap / TilemapRocks " +
            "and nudge the Tilemap Collider 2D (disable + enable) if shapes still look wrong.");
    }
}
#endif

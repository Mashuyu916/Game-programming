using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Logs hints if Play starts without invisible walk tiles painted.
/// </summary>
public static class PlaySceneValidator2D
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Validate()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.name == "TitleScene")
        {
            return; // TitleScene is the startup UI, not the gameplay scene.
        }

        var walk = GameObject.Find("Grid/" + PlatformTilemapLayers2D.DefaultWalkableTilemapName);
        if (walk == null)
        {
            Debug.LogWarning(
                $"Scene is missing Grid/{PlatformTilemapLayers2D.DefaultWalkableTilemapName}. " +
                "Run Tools > Platformer > Setup Custom Tilemaps (background + invisible walk layer).");
            return;
        }

        var tm = walk.GetComponent<Tilemap>();
        if (tm == null)
            return;

        int count = 0;
        foreach (var pos in tm.cellBounds.allPositionsWithin)
        {
            if (tm.HasTile(pos))
                count++;
        }

        if (count == 0)
        {
            Debug.LogWarning(
                $"{PlatformTilemapLayers2D.DefaultWalkableTilemapName} has no invisible walk tiles. " +
                "Use the Tile Palette to paint InvisibleWalk tiles on walkable positions.");
        }
    }
}

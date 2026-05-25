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
        if (scene.name == "SampleScene")
        {
            Debug.LogError(
                "当前运行的是 SampleScene（几乎是空场景）！请打开 Assets/1.unity 再点 Play。");
            return;
        }

        var walk = GameObject.Find("Grid/" + PlatformTilemapLayers2D.DefaultWalkableTilemapName);
        if (walk == null)
        {
            Debug.LogWarning(
                $"场景里没有 {PlatformTilemapLayers2D.DefaultWalkableTilemapName}。" +
                "请在 Unity 菜单执行：Tools → Platformer → 设置自画地图（背景 + 隐形可踩层）。");
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
                $"「{PlatformTilemapLayers2D.DefaultWalkableTilemapName}」上还没有隐形砖。" +
                "用 Tile Palette 选 InvisibleWalk，在可踩位置自己画（看不见，但有碰撞）。");
        }
    }
}

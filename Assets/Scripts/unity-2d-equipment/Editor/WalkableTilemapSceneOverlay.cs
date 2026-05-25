#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Brighter overlay while painting on TilemapWalkable (even if Grid is not selected).
/// </summary>
[InitializeOnLoad]
static class WalkableTilemapSceneOverlay
{
    static WalkableTilemapSceneOverlay()
    {
        SceneView.duringSceneGui += OnSceneGui;
    }

    static void OnSceneGui(SceneView view)
    {
        if (Application.isPlaying)
            return;

        var active = Selection.activeGameObject;
        if (active == null)
            return;

        var tm = active.GetComponent<Tilemap>();
        if (tm == null || tm.name != PlatformTilemapLayers2D.DefaultWalkableTilemapName)
            return;

        var gizmos = active.GetComponent<WalkableTilemapGizmos2D>();
        if (gizmos == null || !gizmos.ShowInSceneView)
            return;

        Handles.color = new Color(0.1f, 1f, 0.45f, 0.55f);
        foreach (var pos in tm.cellBounds.allPositionsWithin)
        {
            if (!tm.HasTile(pos))
                continue;
            var c = tm.GetCellCenterWorld(pos);
            var s = tm.cellSize;
            Handles.DrawSolidRectangleWithOutline(
                new[]
                {
                    c + new Vector3(-s.x * 0.47f, -s.y * 0.47f, 0f),
                    c + new Vector3(s.x * 0.47f, -s.y * 0.47f, 0f),
                    c + new Vector3(s.x * 0.47f, s.y * 0.47f, 0f),
                    c + new Vector3(-s.x * 0.47f, s.y * 0.47f, 0f),
                },
                new Color(0.1f, 1f, 0.4f, 0.2f),
                new Color(0.1f, 1f, 0.5f, 0.95f));
        }

        Handles.Label(
            tm.GetCellCenterWorld(tm.cellBounds.min) + Vector3.up * 0.6f,
            "隐形可踩砖（仅编辑器可见）",
            EditorStyles.boldLabel);
    }
}
#endif

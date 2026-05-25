using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Editor-only green boxes show where you painted invisible walk tiles on this Tilemap.
/// Game build / Play mode: no drawing.
/// </summary>
[DisallowMultipleComponent]
[ExecuteAlways]
public class WalkableTilemapGizmos2D : MonoBehaviour
{
    [Tooltip("Show green wire boxes in Scene view for each invisible tile.")]
    [SerializeField] bool showInSceneView = true;

    [SerializeField] Color gizmoColor = new Color(0.15f, 1f, 0.35f, 0.9f);

    [Tooltip("Semi-transparent fill inside each cell (easier to see).")]
    [SerializeField] bool drawFilled = true;

    public bool ShowInSceneView => showInSceneView;

    Tilemap _tilemap;

    void OnEnable()
    {
        _tilemap = GetComponent<Tilemap>();
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        if (!showInSceneView || Application.isPlaying)
            return;
        DrawCells(false);
    }

    void OnDrawGizmosSelected()
    {
        if (!showInSceneView || Application.isPlaying)
            return;
        DrawCells(true);
    }

    void DrawCells(bool selected)
    {
        if (_tilemap == null)
            _tilemap = GetComponent<Tilemap>();
        if (_tilemap == null)
            return;

        var color = gizmoColor;
        if (selected)
            color = new Color(color.r, color.g, color.b, 1f);
        Gizmos.color = color;

        foreach (var pos in _tilemap.cellBounds.allPositionsWithin)
        {
            if (!_tilemap.HasTile(pos))
                continue;

            var c = _tilemap.GetCellCenterWorld(pos);
            var s = _tilemap.cellSize;
            var size = new Vector3(s.x * 0.94f, s.y * 0.94f, 0.02f);

            if (drawFilled)
            {
                var fill = color;
                fill.a = selected ? 0.35f : 0.22f;
                Gizmos.color = fill;
                Gizmos.DrawCube(c, size);
            }

            Gizmos.color = color;
            Gizmos.DrawWireCube(c, size);
        }
    }
#endif
}

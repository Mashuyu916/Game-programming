using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Rebuilds tilemap colliders on play so custom sprite physics shapes apply reliably.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-90)]
public class PlatformTilemapColliderRefresh2D : MonoBehaviour
{
    void Awake() => Refresh();

    void Start() => Refresh();

    public void Refresh()
    {
        foreach (var col in GetComponentsInChildren<TilemapCollider2D>(true))
        {
            if (col == null || !col.enabled)
                continue;
            col.enabled = false;
            col.enabled = true;
        }

        Physics2D.SyncTransforms();
    }
}

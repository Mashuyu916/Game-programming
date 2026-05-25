using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Ensures tilemap colliders use the "platform" layer so <see cref="PlayerMovement2D"/> can detect ground.
/// Add to the Grid object (parent of Tilemap).
/// </summary>
[DisallowMultipleComponent]
public class PlatformTilemapSetup2D : MonoBehaviour
{
    [SerializeField] string platformLayerName = PlayerMovement2D.PlatformLayerName;

    void Awake()
    {
        Apply();
    }

    void OnValidate()
    {
        Apply();
    }

    public void Apply()
    {
        int layer = LayerMask.NameToLayer(platformLayerName);
        if (layer < 0)
        {
            Debug.LogWarning(
                $"PlatformTilemapSetup2D: Layer \"{platformLayerName}\" not found. " +
                "Edit → Project Settings → Tags and Layers, then set your Tilemap to that layer.",
                this);
            return;
        }

        foreach (var tilemap in GetComponentsInChildren<Tilemap>(true))
        {
            tilemap.gameObject.layer = layer;
            var col = tilemap.GetComponent<TilemapCollider2D>();
            if (col != null && !col.usedByComposite)
                col.enabled = true;
        }
    }
}

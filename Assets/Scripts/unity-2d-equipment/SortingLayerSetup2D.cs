using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Applies a sorting layer / order to renderers on this object (and children).
/// Put on BG sprites or a decoration Tilemap named "TilemapDecor".
/// </summary>
[DisallowMultipleComponent]
public class SortingLayerSetup2D : MonoBehaviour
{
    [SerializeField] string sortingLayerName = "Background";
    [SerializeField] int sortingOrder;

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
        int id = SortingLayer.NameToID(sortingLayerName);
        if (id == 0 && sortingLayerName != "Default")
        {
            Debug.LogWarning(
                $"SortingLayerSetup2D: Sorting layer \"{sortingLayerName}\" not found. " +
                "Add it under Edit → Project Settings → Tags and Layers → Sorting Layers.",
                this);
        }

        foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
        {
            sr.sortingLayerID = id;
            sr.sortingOrder = sortingOrder;
        }

        foreach (var tr in GetComponentsInChildren<TilemapRenderer>(true))
        {
            tr.sortingLayerID = id;
            tr.sortingOrder = sortingOrder;
        }
    }
}

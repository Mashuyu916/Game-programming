using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Visual tilemaps = decoration only (no collision).
/// Walkable collision only on <see cref="InvisibleWalkTileSetup.WalkableTilemapName"/> — paint invisible tiles yourself.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public class PlatformTilemapLayers2D : MonoBehaviour
{
    public const string DefaultWalkableTilemapName = "TilemapWalkable";

    [SerializeField] string[] visualOnlyTilemapNames = { "Tilemap", "TilemapBackground", "TilemapRocks" };
    [SerializeField] string[] walkableTilemapNames = { DefaultWalkableTilemapName };
    [SerializeField] string platformLayerName = PlayerMovement2D.PlatformLayerName;

    [Header("Scene background")]
    [SerializeField] bool applyBlackBackground;
    [SerializeField] Color backgroundColor = Color.black;

    [Header("Optional overrides (off = keep your painted layout)")]
    [SerializeField] bool overrideTilemapSorting;

    void Awake()
    {
        if (applyBlackBackground)
            ApplyBlackBackground();
        ApplyTileColliderRules();
        ApplyCollidersOnly();
    }

    void OnValidate()
    {
        if (!Application.isPlaying)
            ApplyCollidersOnly();
    }

    void ApplyBlackBackground()
    {
        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = backgroundColor;
        }

        RenderSettings.ambientLight = backgroundColor;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;

        var bg = GameObject.Find("BG");
        if (bg != null)
        {
            var sr = bg.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = backgroundColor;
                sr.sortingOrder = -20;
            }
        }
    }

    /// <summary>Only configures layers/colliders; never paints or clears tiles.</summary>
    public void ApplyCollidersOnly()
    {
        int platformLayer = LayerMask.NameToLayer(platformLayerName);
        int defaultSort = SortingLayer.NameToID("Default");
        int bgSort = SortingLayer.NameToID("Background");

        foreach (var tm in GetComponentsInChildren<Tilemap>(true))
        {
            string n = tm.name;
            var col = tm.GetComponent<TilemapCollider2D>();
            var rend = tm.GetComponent<TilemapRenderer>();

            if (IsVisualOnly(n))
            {
                tm.gameObject.layer = LayerMask.NameToLayer("Default");
                if (col != null)
                    col.enabled = false;
                if (overrideTilemapSorting && rend != null)
                {
                    rend.sortingLayerID = n == "TilemapBackground" ? bgSort : defaultSort;
                    rend.sortingOrder = n == "TilemapRocks" ? 1 : 0;
                }
                continue;
            }

            if (!IsWalkableTilemap(n))
                continue;

            if (platformLayer >= 0)
                tm.gameObject.layer = platformLayer;

            if (rend != null)
                rend.enabled = false;

            bool hasTiles = false;
            foreach (var pos in tm.cellBounds.allPositionsWithin)
            {
                if (tm.HasTile(pos))
                {
                    hasTiles = true;
                    break;
                }
            }

            if (col == null && hasTiles)
                col = tm.gameObject.AddComponent<TilemapCollider2D>();

            if (col == null)
                continue;

            col.enabled = hasTiles;
            col.usedByComposite = false;
            col.isTrigger = false;
            if (col.extrusionFactor <= 0f)
                col.extrusionFactor = 0.00001f;
        }

        RefreshColliders();
    }

    public void Apply() => ApplyCollidersOnly();

    bool IsVisualOnly(string name)
    {
        if (visualOnlyTilemapNames == null)
            return false;
        foreach (var v in visualOnlyTilemapNames)
        {
            if (v == name)
                return true;
        }
        return false;
    }

    bool IsWalkableTilemap(string name)
    {
        if (walkableTilemapNames == null)
            return false;
        foreach (var w in walkableTilemapNames)
        {
            if (w == name)
                return true;
        }
        return false;
    }

    void RefreshColliders()
    {
        foreach (var col in GetComponentsInChildren<TilemapCollider2D>(true))
        {
            if (col == null || !col.enabled)
                continue;
            col.ProcessTilemapChanges();
            col.enabled = false;
            col.enabled = true;
        }
        Physics2D.SyncTransforms();
    }

    /// <summary>Decoration tiles = no physics; only InvisibleWalk has collision.</summary>
    public static void ApplyTileColliderRules()
    {
        foreach (var t in Resources.FindObjectsOfTypeAll<Tile>())
        {
            if (t == null || string.IsNullOrEmpty(t.name))
                continue;

            if (t.name == "InvisibleWalk")
                t.colliderType = Tile.ColliderType.Grid;
            else if (t.name.StartsWith("Tiles_"))
                t.colliderType = Tile.ColliderType.None;
        }
    }
}

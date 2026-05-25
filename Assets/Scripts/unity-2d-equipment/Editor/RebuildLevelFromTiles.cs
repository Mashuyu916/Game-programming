#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Builds platformer level from Assets/Art/Tiles.png sprite slices (Tiles_0 … Tiles_25).
/// Menu: Tools → Platformer → Rebuild Level (Tiles.png)
/// </summary>
public static class RebuildLevelFromTiles
{
    const string ScenePath = "Assets/1.unity";
    const string TilesTexturePath = "Assets/Art/Tiles.png";
    const string TileFolder = "Assets/Art/TilesPalette";

    static readonly string[] GrassColliderNames =
    {
        "Tiles_3", "Tiles_4", "Tiles_5", "Tiles_6", "Tiles_7", "Tiles_8",
        "Tiles_9", "Tiles_10", "Tiles_11", "Tiles_12", "Tiles_13", "Tiles_14",
        "Tiles_15", "Tiles_16", "Tiles_17", "Tiles_18",
    };

    static readonly HashSet<string> GrassSet = new HashSet<string>(GrassColliderNames);

    [MenuItem("Tools/Platformer/（模板）Rebuild Level (Tiles.png)")]
    public static void RebuildFromMenu()
    {
        if (!EditorUtility.DisplayDialog(
                "会清空并自动生成关卡",
                "这会删除你在 Tilemap 上画的内容，并自动生成一整张示例地图。\n\n" +
                "若你要自己画地图 + 隐形可踩砖，请取消，改用：\n" +
                "Tools → Platformer → 设置自画地图（背景 + 隐形可踩层）",
                "仍然生成模板",
                "取消"))
            return;
        Rebuild();
    }

    public static void Rebuild()
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "Art/Tiles.png")))
        {
            Debug.LogError("RebuildLevelFromTiles: Tiles.png not found.");
            return;
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var grid = GameObject.Find("Grid");
        if (grid == null)
        {
            Debug.LogError("RebuildLevelFromTiles: Grid not found.");
            return;
        }

        ApplyBlackBackground();
        var tiles = EnsureTileAssets();
        PaintLevel(grid.transform, tiles);
        UpdateLayerConfig(grid);
        PlatformTilemapLayers2D.ApplyTileColliderRules();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("RebuildLevelFromTiles: Level built from Tiles.png slices.");
    }

    static void ApplyBlackBackground()
    {
        var cam = Camera.main;
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
        }

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = Color.black;

        var bg = GameObject.Find("BG");
        if (bg != null)
        {
            var sr = bg.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = Color.black;
                sr.sortingOrder = -20;
            }
        }
    }

    static TileBase[] EnsureTileAssets()
    {
        if (!AssetDatabase.IsValidFolder(TileFolder))
            AssetDatabase.CreateFolder("Assets/Art", "TilesPalette");

        var sprites = AssetDatabase.LoadAllAssetsAtPath(TilesTexturePath)
            .OfType<Sprite>()
            .OrderBy(s =>
            {
                if (s.name.StartsWith("Tiles_") && int.TryParse(s.name.Substring(6), out int n))
                    return n;
                return 999;
            })
            .ToArray();

        var result = new TileBase[26];
        for (int i = 0; i < 26; i++)
        {
            string tileName = $"Tiles_{i}";
            string path = $"{TileFolder}/{tileName}.asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            Sprite sprite = i < sprites.Length ? sprites.FirstOrDefault(s => s.name == tileName) : null;
            if (sprite == null && i < sprites.Length)
                sprite = sprites[i];

            bool grass = GrassSet.Contains(tileName);
            bool platform = tileName == "Tiles_0" || tileName == "Tiles_1" || tileName == "Tiles_2";
            var collider = grass || platform ? Tile.ColliderType.Sprite : Tile.ColliderType.None;

            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, path);
            }

            tile.name = tileName;
            tile.sprite = sprite;
            tile.colliderType = collider;
            EditorUtility.SetDirty(tile);
            result[i] = tile;
        }

        AssetDatabase.SaveAssets();
        return result;
    }

    static void PaintLevel(Transform grid, TileBase[] tiles)
    {
        TileBase T(int i) => tiles[Mathf.Clamp(i, 0, tiles.Length - 1)];

        var walk = grid.Find("Tilemap")?.GetComponent<Tilemap>();
        var bg = grid.Find("TilemapBackground")?.GetComponent<Tilemap>();
        var rocks = grid.Find("TilemapRocks")?.GetComponent<Tilemap>();

        if (walk == null)
        {
            Debug.LogError("RebuildLevelFromTiles: Tilemap missing.");
            return;
        }

        walk.ClearAllTiles();
        bg?.ClearAllTiles();
        rocks?.ClearAllTiles();

        const int groundY = -7;
        const int dirtY = -8;

        for (int x = -12; x <= 44; x++)
        {
            walk.SetTile(new Vector3Int(x, dirtY, 0), T(20 + ((x + 12) % 5)));
            walk.SetTile(new Vector3Int(x, groundY, 0), T(15 + ((x + 12) % 4)));
        }

        void Platform(int x0, int x1, int y)
        {
            for (int x = x0; x <= x1; x++)
                walk.SetTile(new Vector3Int(x, y, 0), T(11 + ((x - x0) % 4)));
        }

        Platform(-9, -5, -4);
        Platform(-2, 3, -4);
        Platform(7, 12, -4);
        Platform(15, 21, -1);
        Platform(26, 32, -1);
        Platform(35, 40, 2);
        Platform(8, 14, 2);
        Platform(-5, -1, -1);
        Platform(18, 23, 4);
        Platform(28, 33, 5);

        walk.SetTile(new Vector3Int(-11, -6, 0), T(3));
        walk.SetTile(new Vector3Int(42, -6, 0), T(4));
        walk.SetTile(new Vector3Int(20, -5, 0), T(0));
        walk.SetTile(new Vector3Int(30, -5, 0), T(1));

        if (bg != null)
        {
            bg.SetTile(new Vector3Int(-14, 2, 0), T(8));
            bg.SetTile(new Vector3Int(-8, 3, 0), T(9));
            bg.SetTile(new Vector3Int(0, 4, 0), T(10));
            bg.SetTile(new Vector3Int(12, 3, 0), T(8));
            bg.SetTile(new Vector3Int(24, 5, 0), T(10));
            bg.SetTile(new Vector3Int(38, 2, 0), T(9));
        }

        if (rocks != null)
        {
            rocks.SetTile(new Vector3Int(-10, -2, 0), T(5));
            rocks.SetTile(new Vector3Int(14, -1, 0), T(2));
            rocks.SetTile(new Vector3Int(22, 1, 0), T(19));
            rocks.SetTile(new Vector3Int(36, 0, 0), T(5));
            rocks.SetTile(new Vector3Int(41, -5, 0), T(2));
        }

        var spawn = GameObject.Find("SpawnPoint");
        if (spawn != null)
            spawn.transform.position = new Vector3(-3f, -6.45f, 0f);

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            player.transform.position = new Vector3(-3f, -6.45f, 0f);
    }

    public static void UpdateLayerConfig(GameObject grid)
    {
        var layers = grid.GetComponent<PlatformTilemapLayers2D>();
        if (layers == null)
            layers = grid.AddComponent<PlatformTilemapLayers2D>();
        if (grid.GetComponent<PlatformTilemapColliderRefresh2D>() == null)
            grid.AddComponent<PlatformTilemapColliderRefresh2D>();

        InvisibleWalkTileSetup.ConfigureLayersComponentPublic(grid);
    }
}
#endif

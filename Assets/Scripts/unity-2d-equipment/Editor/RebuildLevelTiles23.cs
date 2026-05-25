#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Clears old Tiles.png scenery, keeps black BG, builds a platformer level from Tiles2 + Tiles3.
/// Menu: Tools → Platformer → Rebuild Level (Tiles2+3)
/// </summary>
public static class RebuildLevelTiles23
{
    const string ScenePath = "Assets/1.unity";
    const string Tiles2Path = "Assets/Art/Tiles2.png";
    const string Tiles3Path = "Assets/Art/Tiles3.png";
    const string TileFolder = "Assets/Art/LevelTiles23";

    static readonly string[] GrassTileNames =
    {
        "Tiles_3", "Tiles_4", "Tiles_5", "Tiles_6", "Tiles_7",
        "Tiles_8", "Tiles_9", "Tiles_10", "Tiles_11", "Tiles_12",
    };

    [MenuItem("Tools/Platformer/（模板）Rebuild Level (Tiles2+3)")]
    public static void RebuildFromMenu()
    {
        if (!EditorUtility.DisplayDialog(
                "会清空并自动生成关卡",
                "这会删除你在 Tilemap 上画的内容。\n若你要自己画地图，请点取消。",
                "仍然生成模板",
                "取消"))
            return;
        Rebuild();
    }

    /// <summary>Called by Unity batchmode: -executeMethod RebuildLevelTiles23.Rebuild</summary>
    public static void Rebuild()
    {
        if (!File.Exists(Path.Combine(Application.dataPath, "Art/Tiles2.png")))
        {
            Debug.LogError("RebuildLevelTiles23: Tiles2.png not found.");
            return;
        }

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var grid = GameObject.Find("Grid");
        if (grid == null)
        {
            Debug.LogError("RebuildLevelTiles23: Grid not found in scene.");
            return;
        }

        EnsureBlackBackgroundOnly(scene);
        var tiles = CreateOrLoadTiles();
        PaintLevel(grid.transform, tiles);
        UpdateLayerConfig(grid);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("RebuildLevelTiles23: Level rebuilt with Tiles2 + Tiles3 (black background only).");
    }

    static void EnsureBlackBackgroundOnly(Scene scene)
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

        var bgMap = GameObject.Find("TilemapBackground");
        if (bgMap != null)
        {
            var tm = bgMap.GetComponent<Tilemap>();
            if (tm != null)
                tm.ClearAllTiles();
        }
    }

    struct TileSet
    {
        public TileBase[] Ground;
        public TileBase[] Top;
        public TileBase[] Deco;
    }

    static TileSet CreateOrLoadTiles()
    {
        if (!AssetDatabase.IsValidFolder(TileFolder))
            AssetDatabase.CreateFolder("Assets/Art", "LevelTiles23");

        var t3 = LoadSprites(Tiles3Path);
        var t2 = LoadSprites(Tiles2Path);

        var groundSprites = PickGrid64(t3, 5, 2).OrderBy(s => s.rect.x).ToArray();
        var topSprites = groundSprites;
        if (groundSprites.Length >= 10)
        {
            topSprites = groundSprites.Take(5).ToArray();
            groundSprites = groundSprites.Skip(5).Take(5).ToArray();
        }

        var decoSprites = t2
            .Where(s => s.rect.width >= 48 && s.rect.height >= 48)
            .OrderByDescending(s => s.rect.width * s.rect.height)
            .Take(6)
            .ToArray();

        var ground = SaveTiles(groundSprites, 0, true);
        var top = SaveTiles(topSprites.Length > 0 ? topSprites : groundSprites, 5, true);
        var deco = SaveTiles(decoSprites, 20, false);
        AssetDatabase.Refresh();
        return new TileSet { Ground = ground, Top = top, Deco = deco };
    }

    static Sprite[] LoadSprites(string path)
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath(path);
        return assets.OfType<Sprite>().OrderBy(s => s.name).ToArray();
    }

    static List<Sprite> PickGrid64(Sprite[] all, int cols, int rows)
    {
        var grid = all
            .Where(s => Mathf.Approximately(s.rect.width, 64f) && Mathf.Approximately(s.rect.height, 64f))
            .OrderByDescending(s => s.rect.y)
            .ThenBy(s => s.rect.x)
            .ToList();

        if (grid.Count >= cols * rows)
            return grid.Take(cols * rows).ToList();

        return all.Where(s => s.rect.width >= 32 && s.rect.height >= 32)
            .OrderByDescending(s => s.rect.y)
            .ThenBy(s => s.rect.x)
            .Take(cols * rows)
            .ToList();
    }

    static TileBase[] SaveTiles(Sprite[] sprites, int nameOffset, bool walkable)
    {
        var result = new List<TileBase>();
        for (int i = 0; i < sprites.Length; i++)
        {
            string tileName = walkable && i < GrassTileNames.Length
                ? GrassTileNames[i % GrassTileNames.Length]
                : $"Tiles_{nameOffset + i}";

            string assetPath = $"{TileFolder}/{tileName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Tile>(assetPath);
            if (existing != null)
            {
                existing.sprite = sprites[i];
                existing.colliderType = walkable ? Tile.ColliderType.Sprite : Tile.ColliderType.None;
                EditorUtility.SetDirty(existing);
                result.Add(existing);
                continue;
            }

            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprites[i];
            tile.name = tileName;
            tile.colliderType = walkable ? Tile.ColliderType.Sprite : Tile.ColliderType.None;
            AssetDatabase.CreateAsset(tile, assetPath);
            result.Add(tile);
        }

        return result.ToArray();
    }

    static void PaintLevel(Transform grid, TileSet tiles)
    {
        var walk = grid.Find("Tilemap")?.GetComponent<Tilemap>();
        var rocks = grid.Find("TilemapRocks")?.GetComponent<Tilemap>();
        if (walk == null)
        {
            Debug.LogError("RebuildLevelTiles23: Tilemap not found under Grid.");
            return;
        }

        walk.ClearAllTiles();
        if (rocks != null)
            rocks.ClearAllTiles();

        TileBase G(int i) => tiles.Ground[Mathf.Clamp(i, 0, tiles.Ground.Length - 1)];
        TileBase T(int i) => tiles.Top[Mathf.Clamp(i, 0, tiles.Top.Length - 1)];
        TileBase D(int i) => tiles.Deco[Mathf.Clamp(i, 0, tiles.Deco.Length - 1)];

        const int groundY = -7;

        for (int x = -12; x <= 42; x++)
            walk.SetTile(new Vector3Int(x, groundY, 0), G((x + 12) % tiles.Ground.Length));

        void Platform(int x0, int x1, int y, bool useTop = true)
        {
            for (int x = x0; x <= x1; x++)
                walk.SetTile(new Vector3Int(x, y, 0), useTop ? T((x - x0) % tiles.Top.Length) : G(x % tiles.Ground.Length));
        }

        Platform(-8, -4, -4);
        Platform(-1, 3, -4);
        Platform(6, 11, -4);
        Platform(14, 20, -1);
        Platform(24, 30, -1);
        Platform(33, 38, 2);
        Platform(10, 16, 2);
        Platform(-6, -2, -1);
        Platform(18, 22, 4);

        for (int x = -10; x <= -8; x++)
            walk.SetTile(new Vector3Int(x, groundY + 1, 0), G(0));
        for (int x = 28; x <= 32; x += 2)
            walk.SetTile(new Vector3Int(x, -5, 0), G(2));

        if (rocks != null)
        {
            rocks.SetTile(new Vector3Int(-10, -2, 0), D(0));
            rocks.SetTile(new Vector3Int(-9, -2, 0), D(1));
            rocks.SetTile(new Vector3Int(22, 0, 0), D(2));
            rocks.SetTile(new Vector3Int(23, 0, 0), D(3));
            rocks.SetTile(new Vector3Int(35, 3, 0), D(4));
            rocks.SetTile(new Vector3Int(40, -3, 0), D(5));
        }

        var spawn = GameObject.Find("SpawnPoint");
        if (spawn != null)
            spawn.transform.position = new Vector3(-3f, -6.45f, 0f);

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            player.transform.position = new Vector3(-3f, -6.45f, 0f);
    }

    static void UpdateLayerConfig(GameObject grid)
    {
        var layers = grid.GetComponent<PlatformTilemapLayers2D>();
        if (layers == null)
            layers = grid.AddComponent<PlatformTilemapLayers2D>();
        if (grid.GetComponent<PlatformTilemapColliderRefresh2D>() == null)
            grid.AddComponent<PlatformTilemapColliderRefresh2D>();

        var so = new SerializedObject(layers);
        var walkable = so.FindProperty("walkableTilemapNames");
        if (walkable != null)
        {
            walkable.arraySize = 2;
            walkable.GetArrayElementAtIndex(0).stringValue = "Tilemap";
            walkable.GetArrayElementAtIndex(1).stringValue = "TilemapBackground";
        }

        so.FindProperty("applyBlackBackground").boolValue = false;
        so.FindProperty("overrideTilemapSorting").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif

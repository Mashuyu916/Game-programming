#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// One-time scene setup: your art tilemaps = background only; you paint invisible walk tiles on TilemapWalkable.
/// Menu: Tools → Platformer → 设置自画地图（背景 + 隐形可踩层）
/// Does NOT paint any level art.
/// </summary>
public static class InvisibleWalkTileSetup
{
    public const string TileAssetName = "InvisibleWalk";
    public const string WalkableTilemapName = "TilemapWalkable";
    const string ScenePath = "Assets/1.unity";
    const string TilePath = "Assets/Art/InvisibleWalkTile.asset";
    const string SpritePath = "Assets/Art/InvisibleWalk.png";

    [MenuItem("Tools/Platformer/设置自画地图（背景 + 隐形可踩层）")]
    public static void SetupFromMenu() => Setup(true);

    public static void Setup(bool openScene)
    {
        if (openScene)
        {
            var active = EditorSceneManager.GetActiveScene().path;
            if (string.IsNullOrEmpty(active) || !active.Replace('\\', '/').EndsWith("1.unity"))
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        EnsureInvisibleTileAsset();
        var grid = GameObject.Find("Grid");
        if (grid == null)
        {
            Debug.LogError("InvisibleWalkTileSetup: 场景里没有 Grid。");
            return;
        }

        EnsureWalkableTilemap(grid);
        ConfigureLayersComponentPublic(grid);
        PlatformTilemapLayers2D.ApplyTileColliderRules();
        grid.GetComponent<PlatformTilemapLayers2D>()?.ApplyCollidersOnly();

        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();

        var walk = grid.transform.Find(WalkableTilemapName);
        if (walk != null && walk.GetComponent<WalkableTilemapGizmos2D>() == null)
            walk.gameObject.AddComponent<WalkableTilemapGizmos2D>();

        Debug.Log(
            "已设置：Tilemap / TilemapBackground / TilemapRocks = 仅装饰（不可踩）。" +
            $"请在 Grid → {WalkableTilemapName} 上用 InvisibleWalk 铺可踩区域。" +
            "选中 TilemapWalkable 时 Scene 会显示绿色格子预览（仅编辑器）。");
    }

    static void EnsureInvisibleTileAsset()
    {
        if (!Directory.Exists("Assets/Art"))
            AssetDatabase.CreateFolder("Assets", "Art");

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        if (sprite == null)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, new Color(1f, 1f, 1f, 0f));
            tex.Apply();
            File.WriteAllBytes(SpritePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(SpritePath);
            var imp = AssetImporter.GetAtPath(SpritePath) as TextureImporter;
            if (imp != null)
            {
                imp.textureType = TextureImporterType.Sprite;
                imp.spriteImportMode = SpriteImportMode.Single;
                imp.alphaIsTransparency = true;
                imp.SaveAndReimport();
            }

            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        }

        var tile = AssetDatabase.LoadAssetAtPath<Tile>(TilePath);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<Tile>();
            AssetDatabase.CreateAsset(tile, TilePath);
        }

        tile.name = TileAssetName;
        tile.sprite = sprite;
        tile.colliderType = Tile.ColliderType.Grid;
        tile.color = new Color(1f, 1f, 1f, 0f);
        EditorUtility.SetDirty(tile);
    }

    static void EnsureWalkableTilemap(GameObject grid)
    {
        var existing = grid.transform.Find(WalkableTilemapName);
        if (existing != null)
            return;

        var go = new GameObject(WalkableTilemapName);
        go.transform.SetParent(grid.transform, false);
        go.AddComponent<Tilemap>();
        go.AddComponent<TilemapCollider2D>();
        go.AddComponent<WalkableTilemapGizmos2D>();
    }

    [MenuItem("Tools/Platformer/显示/隐藏 隐形砖 Scene 预览")]
    static void ToggleGizmoPreview()
    {
        var walk = GameObject.Find("Grid/" + WalkableTilemapName);
        if (walk == null)
        {
            EditorUtility.DisplayDialog("提示", "请先执行「设置自画地图（背景 + 隐形可踩层）」。", "好的");
            return;
        }

        var g = walk.GetComponent<WalkableTilemapGizmos2D>();
        if (g == null)
            g = walk.AddComponent<WalkableTilemapGizmos2D>();

        var so = new SerializedObject(g);
        var prop = so.FindProperty("showInSceneView");
        prop.boolValue = !prop.boolValue;
        so.ApplyModifiedPropertiesWithoutUndo();
        SceneView.RepaintAll();
        Debug.Log(prop.boolValue ? "已开启隐形砖 Scene 绿色预览。" : "已关闭隐形砖 Scene 预览。");
    }

    public static void ConfigureLayersComponentPublic(GameObject grid)
    {
        var layers = grid.GetComponent<PlatformTilemapLayers2D>();
        if (layers == null)
            layers = grid.AddComponent<PlatformTilemapLayers2D>();
        if (grid.GetComponent<PlatformTilemapColliderRefresh2D>() == null)
            grid.AddComponent<PlatformTilemapColliderRefresh2D>();

        var so = new SerializedObject(layers);
        SetStringArray(so, "visualOnlyTilemapNames",
            "Tilemap", "TilemapBackground", "TilemapRocks");
        SetStringArray(so, "walkableTilemapNames", WalkableTilemapName);
        so.FindProperty("applyBlackBackground").boolValue = false;
        so.FindProperty("overrideTilemapSorting").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetStringArray(SerializedObject so, string propName, params string[] values)
    {
        var prop = so.FindProperty(propName);
        if (prop == null)
            return;
        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            prop.GetArrayElementAtIndex(i).stringValue = values[i];
    }
}
#endif

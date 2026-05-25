#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// Old menu removed — use <see cref="InvisibleWalkTileSetup"/> instead.
/// </summary>
public static class BakeWalkableOnGreenGround
{
    [MenuItem("Tools/Platformer/在绿地上铺可站立地砖", true)]
    static bool HideOldMenu() => false;

    [MenuItem("Tools/Platformer/（已停用）在绿地上铺可站立地砖 → 请用「设置自画地图」")]
    static void Redirect()
    {
        EditorUtility.DisplayDialog(
            "已停用自动铺关卡",
            "不会再自动画草地/平台。\n\n" +
            "请使用：Tools → Platformer → 设置自画地图（背景 + 隐形可踩层）\n" +
            "然后在 Grid → TilemapWalkable 上用 InvisibleWalk 砖自己铺可踩区域。",
            "好的");
        InvisibleWalkTileSetup.SetupFromMenu();
    }
}
#endif

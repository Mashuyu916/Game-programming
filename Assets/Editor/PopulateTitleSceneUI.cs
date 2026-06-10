#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor.SceneManagement;

public static class PopulateTitleSceneUI
{
    [MenuItem("Tools/TitleScene/Populate Title UI")]
    public static void CreateTitleUI()
    {
        var root = GameObject.Find("TitleCanvas");

        // determine target scene/index first (prefer Assets/1.unity)
        string targetScene = "";
        int targetIndex = -1;
        var buildScenes = UnityEditor.EditorBuildSettings.scenes;

        // If a scene asset exists at Assets/1.unity, ensure it's in Build Settings and prefer it
        string preferredPath = "Assets/1.unity";
        var preferredSceneAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(preferredPath);
        if (preferredSceneAsset != null)
        {
            // look for it in build scenes
            for (int i = 0; i < buildScenes.Length; i++)
            {
                if (!buildScenes[i].enabled) continue;
                if (buildScenes[i].path.Replace("\\", "/").EndsWith(preferredPath) || buildScenes[i].path == preferredPath)
                {
                    targetScene = System.IO.Path.GetFileNameWithoutExtension(buildScenes[i].path);
                    targetIndex = i;
                    break;
                }
            }

            // if not found in build settings, append it
            if (targetIndex == -1)
            {
                var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(buildScenes);
                var newScene = new EditorBuildSettingsScene(preferredPath, true);
                list.Add(newScene);
                UnityEditor.EditorBuildSettings.scenes = list.ToArray();
                targetIndex = list.Count - 1;
                targetScene = System.IO.Path.GetFileNameWithoutExtension(preferredPath);
                buildScenes = UnityEditor.EditorBuildSettings.scenes; // refresh
            }
        }

        // fallback: prefer any build-settings scene named "1.unity", otherwise first non-TitleScene
        if (targetIndex == -1)
        {
            for (int i = 0; i < buildScenes.Length; i++)
            {
                var s = buildScenes[i];
                if (!s.enabled) continue;
                if (s.path.Replace("\\", "/").EndsWith("/1.unity") || s.path.EndsWith("1.unity"))
                {
                    targetScene = System.IO.Path.GetFileNameWithoutExtension(s.path);
                    targetIndex = i;
                    break;
                }
            }
        }
        if (targetIndex == -1)
        {
            for (int i = 0; i < buildScenes.Length; i++)
            {
                var s = buildScenes[i];
                if (!s.enabled) continue;
                if (s.path.EndsWith("TitleScene.unity")) continue;
                targetScene = System.IO.Path.GetFileNameWithoutExtension(s.path);
                targetIndex = i;
                break;
            }
        }

        if (root != null)
        {
            Debug.Log("TitleCanvas already exists in scene. Updating TitleScreen if present.");
            var existingHolder = GameObject.Find("TitleScreen");
            if (existingHolder == null)
            {
                existingHolder = new GameObject("TitleScreen");
                existingHolder.transform.SetParent(null, false);
                existingHolder.AddComponent<TitleScreenUI>().enabled = true;
            }

            var existingPopup = existingHolder.GetComponent<TitlePopupController>();
            if (existingPopup == null) existingPopup = existingHolder.AddComponent<TitlePopupController>();
            if (!string.IsNullOrEmpty(targetScene))
            {
                existingPopup.sceneToLoad = targetScene;
                existingPopup.sceneBuildIndex = targetIndex;
            }

            // rebind buttons if present
            var startBtnObj = GameObject.Find("StartButton");
            if (startBtnObj != null)
            {
                var b = startBtnObj.GetComponent<UnityEngine.UI.Button>();
                if (b != null)
                {
                    b.onClick.RemoveAllListeners();
                    b.onClick.AddListener(existingPopup.StartGame);
                }
            }
            var quitBtnObj = GameObject.Find("QuitButton");
            if (quitBtnObj != null)
            {
                var b = quitBtnObj.GetComponent<UnityEngine.UI.Button>();
                if (b != null)
                {
                    b.onClick.RemoveAllListeners();
                    b.onClick.AddListener(existingPopup.QuitGame);
                }
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("TitleScreen updated in scene.");
            return;
        }

        // Create Canvas
        var canvasGO = new GameObject("TitleCanvas", typeof(RectTransform));
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();

        // Panel
        var panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(canvasGO.transform, false);
        var panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.06f, 0.09f, 0.2f, 0.92f);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(820f, 520f);
        panelRect.anchoredPosition = Vector2.zero;

        // Title Text
        var title = new GameObject("Title", typeof(RectTransform));
        title.transform.SetParent(panel.transform, false);
        var titleText = title.AddComponent<Text>();
        titleText.text = "我的游戏";
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.fontSize = 68;
        titleText.color = Color.white;
        titleText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        var titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.86f);
        titleRect.anchorMax = new Vector2(0.5f, 0.86f);
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta = new Vector2(700f, 120f);

        // Subtitle
        var subtitle = new GameObject("Subtitle", typeof(RectTransform));
        subtitle.transform.SetParent(panel.transform, false);
        var subtitleText = subtitle.AddComponent<Text>();
        subtitleText.text = "欢迎来到冒险世界";
        subtitleText.alignment = TextAnchor.MiddleCenter;
        subtitleText.fontSize = 26;
        subtitleText.color = new Color(0.7f, 0.85f, 0.98f, 1f);
        subtitleText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        var subRect = subtitle.GetComponent<RectTransform>();
        subRect.anchorMin = new Vector2(0.5f, 0.76f);
        subRect.anchorMax = new Vector2(0.5f, 0.76f);
        subRect.anchoredPosition = Vector2.zero;
        subRect.sizeDelta = new Vector2(700f, 80f);

        // Buttons group
        var buttonGroup = new GameObject("ButtonGroup", typeof(RectTransform));
        buttonGroup.transform.SetParent(panel.transform, false);
        var groupRect = buttonGroup.GetComponent<RectTransform>();
        groupRect.anchorMin = new Vector2(0.5f, 0.35f);
        groupRect.anchorMax = new Vector2(0.5f, 0.35f);
        groupRect.sizeDelta = new Vector2(380f, 70f * 2 + 22f);
        groupRect.anchoredPosition = Vector2.zero;

        // Start Button
        var startBtn = CreateButton("StartButton", "开始游戏", buttonGroup.transform);
        var startRect = startBtn.GetComponent<RectTransform>();
        startRect.anchorMin = new Vector2(0.5f, 1f);
        startRect.anchorMax = new Vector2(0.5f, 1f);
        startRect.anchoredPosition = Vector2.zero;

        // Quit Button
        var quitBtn = CreateButton("QuitButton", "退出游戏", buttonGroup.transform);
        var quitRect = quitBtn.GetComponent<RectTransform>();
        quitRect.anchorMin = new Vector2(0.5f, 0f);
        quitRect.anchorMax = new Vector2(0.5f, 0f);
        quitRect.anchoredPosition = Vector2.zero;

        // Footer
        var footer = new GameObject("Footer", typeof(RectTransform));
        footer.transform.SetParent(panel.transform, false);
        var footerText = footer.AddComponent<Text>();
        footerText.text = "按 Enter 或 点击开始";
        footerText.alignment = TextAnchor.MiddleCenter;
        footerText.fontSize = 18;
        footerText.color = new Color(0.72f, 0.82f, 1f, 0.8f);
        footerText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        var footRect = footer.GetComponent<RectTransform>();
        footRect.anchorMin = new Vector2(0.5f, 0.12f);
        footRect.anchorMax = new Vector2(0.5f, 0.12f);
        footRect.anchoredPosition = Vector2.zero;
        footRect.sizeDelta = new Vector2(600f, 40f);

        // Version
        var version = new GameObject("Version", typeof(RectTransform));
        version.transform.SetParent(panel.transform, false);
        var versionText = version.AddComponent<Text>();
        versionText.text = "v1.0";
        versionText.alignment = TextAnchor.LowerRight;
        versionText.fontSize = 14;
        versionText.color = new Color(0.72f, 0.82f, 1f, 0.8f);
        versionText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        var verRect = version.GetComponent<RectTransform>();
        verRect.anchorMin = new Vector2(0.95f, 0.04f);
        verRect.anchorMax = new Vector2(0.95f, 0.04f);
        verRect.anchoredPosition = Vector2.zero;
        verRect.sizeDelta = new Vector2(200f, 24f);

        // Attach TitleScreenUI and TitlePopupController to a root object for configuration
        var holder = new GameObject("TitleScreen");
        holder.transform.SetParent(null, false);
        var ts = holder.AddComponent<TitleScreenUI>();
        ts.enabled = true;

        var popup = holder.AddComponent<TitlePopupController>();
        if (!string.IsNullOrEmpty(targetScene))
        {
            popup.sceneToLoad = targetScene;
            popup.sceneBuildIndex = targetIndex;
        }

        // wire buttons
        startBtn.onClick.AddListener(popup.StartGame);
        quitBtn.onClick.AddListener(popup.QuitGame);

        // Select the created canvas
        Selection.activeGameObject = canvasGO;

        // Mark scene dirty and save
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("Title UI populated into scene (TitleCanvas).");
    }

    static Button CreateButton(string name, string label, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.08f, 0.55f, 0.95f, 1f);
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(380f, 70f);

        var btn = go.AddComponent<Button>();

        var txtGO = new GameObject("Text", typeof(RectTransform));
        txtGO.transform.SetParent(go.transform, false);
        var txt = txtGO.AddComponent<Text>();
        txt.text = label;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.fontSize = 26;
        txt.color = Color.white;
        txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        var txtRect = txtGO.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.one * 0.5f;
        txtRect.anchorMax = Vector2.one * 0.5f;
        txtRect.anchoredPosition = Vector2.zero;
        txtRect.sizeDelta = rect.sizeDelta;

        return btn;
    }
}
#endif

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TitleScreenUI : MonoBehaviour
{
    [Header("Text")]
    public string titleText = "迷失之境";
    public string subtitleText = "踏入未知，开启属于你的冒险";
    public string startButtonText = "开始游戏";
    public string quitButtonText = "退出游戏";
    public string versionText = "DEMO  v1.0";
    public string footerText = "按 Enter 开始游戏  ·  Esc 退出";

    [Header("Scene")]
    public string sceneToLoad = "1";

    [Header("Colors")]
    public Color backgroundColor = new Color(0.025f, 0.045f, 0.1f, 1f);
    public Color accentColor = new Color(0.16f, 0.68f, 0.92f, 1f);
    public Color panelColor = new Color(0.035f, 0.075f, 0.15f, 0.94f);
    public Color buttonColor = new Color(0.08f, 0.52f, 0.78f, 1f);
    public Color buttonTextColor = Color.white;
    public Color titleColor = Color.white;
    public Color subtitleColor = new Color(0.7f, 0.85f, 0.98f, 1f);
    public Color footerColor = new Color(0.72f, 0.82f, 1f, 0.8f);

    [Header("Layout")]
    public Vector2 panelSize = new Vector2(860f, 560f);
    public float buttonWidth = 380f;
    public float buttonHeight = 70f;
    public float spacing = 22f;

    Font _font;
    Sprite _uiSprite = null;
    CanvasGroup _screenGroup;
    RectTransform _panelRect;
    RectTransform _startButtonRect;
    Image _startButtonImage;
    Image _loadingOverlay;
    bool _isLoading;
    float _createdAt;

    void Awake()
    {
        _font = null;
        try
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        catch { }
        if (_font == null)
        {
            try { _font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { _font = null; }
        }
        if (_font == null)
        {
            _font = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei", "SimHei", "Arial" }, 14);
        }

        _uiSprite = null;

        CreateEventSystemIfNeeded();
        if (GameObject.Find("TitleCanvas") == null)
            CreateTitleScreen();
        _createdAt = Time.unscaledTime;
    }

    void Update()
    {
        AnimateScreen();

        if (_isLoading)
            return;

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
            StartGame();

        if (Input.GetKeyDown(KeyCode.Escape))
            QuitGame();
    }

    void CreateEventSystemIfNeeded()
    {
        if (FindObjectOfType<EventSystem>() != null)
            return;

        var eventSystem = new GameObject("EventSystem");
        eventSystem.transform.SetParent(transform, false);
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<StandaloneInputModule>();
    }

    void CreateTitleScreen()
    {
        var canvasGO = new GameObject("TitleCanvas");
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
        canvasGO.GetComponent<CanvasScaler>().matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();
        _screenGroup = canvasGO.AddComponent<CanvasGroup>();
        _screenGroup.alpha = 0f;

        CreateBackground(canvasGO.transform);
    }

    void CreateBackground(Transform parent)
    {
        var background = CreateUIObject("Background", parent);
        var image = background.AddComponent<Image>();
        image.color = backgroundColor;
        var rect = background.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        CreateStretchImage("SkyGlow", background.transform,
            new Vector2(0f, 0.66f), Vector2.one,
            new Color(accentColor.r, accentColor.g, accentColor.b, 0.18f));
        CreateStretchImage("HorizonGlow", background.transform,
            new Vector2(0f, 0.42f), new Vector2(1f, 0.68f),
            new Color(0.2f, 0.45f, 0.74f, 0.12f));
        CreateStretchImage("BottomShade", background.transform,
            Vector2.zero, new Vector2(1f, 0.24f),
            new Color(0f, 0.01f, 0.04f, 0.42f));

        CreateDecorations(background.transform);

        var panel = CreateUIObject("Panel", background.transform);
        var panelImage = panel.AddComponent<Image>();
        panelImage.color = panelColor;
        if (_uiSprite != null)
        {
            panelImage.sprite = _uiSprite;
            panelImage.type = Image.Type.Sliced;
        }
        _panelRect = panel.GetComponent<RectTransform>();
        _panelRect.sizeDelta = panelSize;
        _panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        _panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        _panelRect.anchoredPosition = Vector2.zero;

        CreatePanelBorder(panel.transform);

        var eyebrow = CreateText("Eyebrow", "A  2 D   A D V E N T U R E", 16,
            panel.transform, new Vector2(0.5f, 0.94f));
        eyebrow.color = new Color(accentColor.r, accentColor.g, accentColor.b, 0.9f);

        var header = CreateText("Title", titleText, 68, panel.transform, new Vector2(0.5f, 0.86f));
        header.color = titleColor;
        header.fontStyle = FontStyle.Bold;
        AddOutline(header.gameObject, new Color(0f, 0f, 0f, 0.4f));

        var subtitle = CreateText("Subtitle", subtitleText, 26, panel.transform, new Vector2(0.5f, 0.76f));
        subtitle.color = subtitleColor;

        CreateStretchImage("Line", panel.transform,
            new Vector2(0.1f, 0.705f), new Vector2(0.9f, 0.705f),
            new Color(1f, 1f, 1f, 0.16f), new Vector2(0f, 2f));

        var buttonGroup = CreateUIObject("ButtonGroup", panel.transform);
        var groupRect = buttonGroup.GetComponent<RectTransform>();
        groupRect.anchorMin = new Vector2(0.5f, 0.35f);
        groupRect.anchorMax = new Vector2(0.5f, 0.35f);
        groupRect.sizeDelta = new Vector2(buttonWidth, buttonHeight * 2 + spacing);
        groupRect.anchoredPosition = Vector2.zero;

        var startButton = CreateButton("StartButton", startButtonText, buttonGroup.transform, new Vector2(0.5f, 1f), new Vector2(buttonWidth, buttonHeight));
        startButton.onClick.AddListener(StartGame);
        _startButtonRect = startButton.GetComponent<RectTransform>();
        _startButtonImage = startButton.GetComponent<Image>();

        var quitButton = CreateButton("QuitButton", quitButtonText, buttonGroup.transform, new Vector2(0.5f, 0f), new Vector2(buttonWidth, buttonHeight));
        quitButton.onClick.AddListener(QuitGame);

        var footer = CreateText("Footer", footerText, 18, panel.transform, new Vector2(0.5f, 0.12f));
        footer.color = footerColor;

        var version = CreateText("Version", versionText, 18, panel.transform, new Vector2(0.95f, 0.04f));
        version.color = footerColor;
        version.alignment = TextAnchor.LowerRight;

        var overlay = CreateStretchImage("LoadingOverlay", background.transform,
            Vector2.zero, Vector2.one, new Color(0f, 0.02f, 0.06f, 0f));
        _loadingOverlay = overlay.GetComponent<Image>();
    }

    Button CreateButton(string name, string text, Transform parent, Vector2 anchor, Vector2 size)
    {
        var buttonGO = CreateUIObject(name, parent);
        var rect = buttonGO.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;

        var image = buttonGO.AddComponent<Image>();
        image.color = buttonColor;
        if (_uiSprite != null)
        {
            image.sprite = _uiSprite;
            image.type = Image.Type.Sliced;
        }

        var button = buttonGO.AddComponent<Button>();
        var colors = button.colors;
        colors.normalColor = buttonColor;
        colors.highlightedColor = new Color(
            Mathf.Min(1f, buttonColor.r + 0.08f),
            Mathf.Min(1f, buttonColor.g + 0.08f),
            Mathf.Min(1f, buttonColor.b + 0.08f), 1f);
        colors.pressedColor = new Color(buttonColor.r * 0.85f, buttonColor.g * 0.85f, buttonColor.b * 0.85f, 0.9f);
        colors.selectedColor = new Color(buttonColor.r * 1.05f, buttonColor.g * 1.05f, buttonColor.b * 1.05f, 1f);
        button.colors = colors;

        var label = CreateText("Text", text, 26, buttonGO.transform, Vector2.one * 0.5f);
        label.color = buttonTextColor;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;

        AddOutline(buttonGO, new Color(1f, 1f, 1f, 0.08f));
        return button;
    }

    void CreateDecorations(Transform parent)
    {
        for (int i = 0; i < 18; i++)
        {
            var star = CreateUIObject("Light_" + i, parent);
            var image = star.AddComponent<Image>();
            image.color = new Color(0.75f, 0.9f, 1f, 0.08f + (i % 4) * 0.035f);
            var rect = star.GetComponent<RectTransform>();
            float x = ((i * 37) % 100) / 100f;
            float y = 0.18f + ((i * 53) % 74) / 100f;
            rect.anchorMin = rect.anchorMax = new Vector2(x, y);
            float size = 2f + (i % 3) * 2f;
            rect.sizeDelta = new Vector2(size, size);
        }
    }

    void CreatePanelBorder(Transform parent)
    {
        CreateStretchImage("TopBorder", parent, new Vector2(0f, 1f), Vector2.one,
            new Color(accentColor.r, accentColor.g, accentColor.b, 0.52f), new Vector2(0f, 3f));
        CreateStretchImage("BottomBorder", parent, Vector2.zero, new Vector2(1f, 0f),
            new Color(accentColor.r, accentColor.g, accentColor.b, 0.2f), new Vector2(0f, 2f));
    }

    GameObject CreateStretchImage(string name, Transform parent, Vector2 anchorMin,
        Vector2 anchorMax, Color color, Vector2? sizeDelta = null)
    {
        var go = CreateUIObject(name, parent);
        var image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        if (sizeDelta.HasValue)
            rect.sizeDelta = sizeDelta.Value;
        return go;
    }

    void AnimateScreen()
    {
        float elapsed = Time.unscaledTime - _createdAt;

        if (_screenGroup != null)
            _screenGroup.alpha = Mathf.Clamp01(elapsed / 0.55f);

        if (_panelRect != null)
            _panelRect.anchoredPosition = new Vector2(0f, Mathf.Sin(elapsed * 0.85f) * 4f);

        if (_startButtonRect != null && !_isLoading)
        {
            float pulse = 1f + Mathf.Sin(elapsed * 2.2f) * 0.012f;
            _startButtonRect.localScale = new Vector3(pulse, pulse, 1f);
        }

        if (_startButtonImage != null && !_isLoading)
        {
            float glow = 0.94f + Mathf.Sin(elapsed * 2.2f) * 0.06f;
            _startButtonImage.color = new Color(
                buttonColor.r * glow, buttonColor.g * glow, buttonColor.b * glow, 1f);
        }
    }

    Text CreateText(string name, string content, int size, Transform parent, Vector2 anchor)
    {
        var textGO = CreateUIObject(name, parent);
        var rect = textGO.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(panelSize.x - 80f, 120f);

        var text = textGO.AddComponent<Text>();
        text.text = content;
        text.font = _font;
        text.fontSize = size;
        text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    GameObject CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    void AddOutline(GameObject target, Color color)
    {
        var outline = target.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = new Vector2(1.5f, -1.5f);
    }

    public void StartGame()
    {
        if (_isLoading)
            return;

        if (string.IsNullOrEmpty(sceneToLoad))
        {
            Debug.LogWarning("TitleScreenUI: sceneToLoad 未配置，请在 Inspector 中填写要加载的场景名。");
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneToLoad))
        {
            Debug.LogError("TitleScreenUI: 找不到场景 " + sceneToLoad +
                "，请确认它已添加到 Build Settings。");
            return;
        }

        StartCoroutine(LoadGame());
    }

    System.Collections.IEnumerator LoadGame()
    {
        _isLoading = true;
        float elapsed = 0f;

        while (elapsed < 0.45f)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / 0.45f);
            if (_screenGroup != null)
                _screenGroup.alpha = 1f - t;
            if (_loadingOverlay != null)
                _loadingOverlay.color = new Color(0f, 0.02f, 0.06f, t);
            yield return null;
        }

        SceneManager.LoadScene(sceneToLoad);
    }

    public void QuitGame()
    {
        Application.Quit();
        Debug.Log("TitleScreenUI: 退出游戏（编辑器中不会关闭）。");
    }
}

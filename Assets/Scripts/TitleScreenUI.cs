using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TitleScreenUI : MonoBehaviour
{
    [Header("Cover")]
    public string coverResourceName = "TitleCover";
    public Color fallbackBackground = new Color(0.02f, 0.18f, 0.3f, 1f);

    [Header("Start")]
    public string startButtonText = "START GAME";
    public string sceneToLoad = "1";
    public Color buttonColor = new Color(1f, 0.57f, 0.05f, 1f);
    public Color buttonHighlightColor = new Color(1f, 0.75f, 0.16f, 1f);
    public Color buttonPressedColor = new Color(0.9f, 0.36f, 0.03f, 1f);

    CanvasGroup _screenGroup;
    RectTransform _startButtonRect;
    Image _startButtonImage;
    Image _loadingOverlay;
    GameObject _howToPlayPanel;
    bool _isLoading;
    float _createdAt;

    void Awake()
    {
        RemoveExistingCanvas();
        CreateEventSystemIfNeeded();
        CreateTitleScreen();
        _createdAt = Time.unscaledTime;
    }

    void Update()
    {
        AnimateScreen();

        if (_isLoading)
            return;

        if (_howToPlayPanel != null && _howToPlayPanel.activeSelf)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
                CloseHowToPlay();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
            StartGame();

        if (Input.GetKeyDown(KeyCode.Escape))
            QuitGame();
    }

    void RemoveExistingCanvas()
    {
        var existing = GameObject.Find("TitleCanvas");
        if (existing != null)
        {
            existing.SetActive(false);
            Destroy(existing);
        }
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
        var canvasGO = new GameObject("TitleCanvas", typeof(RectTransform));
        canvasGO.transform.SetParent(transform, false);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        _screenGroup = canvasGO.AddComponent<CanvasGroup>();
        _screenGroup.alpha = 0f;

        CreateCover(canvasGO.transform);
        CreateBottomShade(canvasGO.transform);
        CreateMenuButtons(canvasGO.transform);
        CreateHowToPlayPanel(canvasGO.transform);
        CreateLoadingOverlay(canvasGO.transform);
    }

    void CreateCover(Transform parent)
    {
        var coverGO = CreateUIObject("Cover", parent);
        var cover = coverGO.AddComponent<RawImage>();
        cover.color = Color.white;
        cover.raycastTarget = false;

        var texture = Resources.Load<Texture2D>(coverResourceName);
        if (texture != null)
        {
            cover.texture = texture;
        }
        else
        {
            cover.color = fallbackBackground;
            Debug.LogWarning(
                "TitleScreenUI: cover not found. Place the image at Assets/Resources/" +
                coverResourceName + ".png");
        }

        Stretch(cover.rectTransform);
        var fitter = coverGO.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = texture != null ? (float)texture.width / texture.height : 1.5f;
    }

    void CreateBottomShade(Transform parent)
    {
        var shadeGO = CreateUIObject("BottomShade", parent);
        var shade = shadeGO.AddComponent<Image>();
        shade.color = new Color(0.01f, 0.035f, 0.05f, 0.55f);
        shade.raycastTarget = false;

        var rect = shade.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(1f, 0.24f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    void CreateMenuButtons(Transform parent)
    {
        var startButton = CreateMenuButton(
            "StartButton", startButtonText, parent, new Vector2(-220f, 0f), buttonColor, StartGame);
        _startButtonRect = startButton.GetComponent<RectTransform>();
        _startButtonImage = startButton.GetComponent<Image>();

        CreateMenuButton(
            "HowToPlayButton",
            "HOW TO PLAY",
            parent,
            new Vector2(220f, 0f),
            new Color(0.05f, 0.45f, 0.7f, 1f),
            OpenHowToPlay);
    }

    Button CreateMenuButton(string name, string labelText, Transform parent, Vector2 offset,
        Color normalColor, UnityEngine.Events.UnityAction action)
    {
        var buttonGO = CreateUIObject(name, parent);
        var rect = buttonGO.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.075f);
        rect.anchorMax = new Vector2(0.5f, 0.075f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = offset;
        rect.sizeDelta = new Vector2(390f, 76f);

        var image = buttonGO.AddComponent<Image>();
        image.color = normalColor;

        var outline = buttonGO.AddComponent<Outline>();
        outline.effectColor = new Color(0.02f, 0.025f, 0.04f, 1f);
        outline.effectDistance = new Vector2(6f, -6f);

        var button = buttonGO.AddComponent<Button>();
        var colors = button.colors;
        colors.normalColor = normalColor;
        colors.highlightedColor = Color.Lerp(normalColor, Color.white, 0.25f);
        colors.pressedColor = Color.Lerp(normalColor, Color.black, 0.2f);
        colors.selectedColor = colors.highlightedColor;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.onClick.AddListener(action);

        var labelGO = CreateUIObject("Text", buttonGO.transform);
        var label = labelGO.AddComponent<Text>();
        label.text = labelText;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 32;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(1f, 1f, 0.92f, 1f);
        label.raycastTarget = false;
        Stretch(label.rectTransform);

        var textOutline = labelGO.AddComponent<Outline>();
        textOutline.effectColor = new Color(0.04f, 0.02f, 0.04f, 1f);
        textOutline.effectDistance = new Vector2(3f, -3f);
        return button;
    }

    void CreateHowToPlayPanel(Transform parent)
    {
        _howToPlayPanel = CreateUIObject("HowToPlayPanel", parent);
        Stretch(_howToPlayPanel.GetComponent<RectTransform>());

        var dimmer = _howToPlayPanel.AddComponent<Image>();
        dimmer.color = new Color(0.005f, 0.015f, 0.025f, 0.84f);

        var panel = CreateUIObject("Instructions", _howToPlayPanel.transform);
        var panelImage = panel.AddComponent<Image>();
        panelImage.color = new Color(0.025f, 0.095f, 0.14f, 0.98f);

        var panelOutline = panel.AddComponent<Outline>();
        panelOutline.effectColor = new Color(1f, 0.58f, 0.08f, 1f);
        panelOutline.effectDistance = new Vector2(4f, -4f);

        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(1120f, 760f);

        CreateInstructionText(
            "Heading",
            "HOW TO PLAY",
            panel.transform,
            new Vector2(0f, 300f),
            new Vector2(900f, 70f),
            42,
            TextAnchor.MiddleCenter,
            new Color(1f, 0.65f, 0.08f, 1f),
            FontStyle.Bold);

        string leftColumn =
            "<b>STORY: ESCAPE FROM THE THUNDER PRISON</b>\n" +
            "The Thunder Lord stole the Skyfire Core\n" +
            "and sealed the realm beneath an endless\n" +
            "storm. You are the last samurai to reach\n" +
            "his prison and reclaim its living flame.\n" +
            "Now the Core is in your hands, but every\n" +
            "bolt in the sky has been ordered to hunt\n" +
            "you down before you reach the Sky Gate.\n\n" +
            "<b>CONTROLS</b>\n" +
            "SPACE / K  -  Start the run\n" +
            "SPACE / K  -  Jump\n" +
            "Press again in the air with Double Jump\n" +
            "W / S  -  Move up or down during Flight\n\n" +
            "<b>SURVIVE</b>\n" +
            "Carry the Skyfire Core beyond the storm.\n" +
            "Falling into a gap ends the run.";

        string rightColumn =
            "<b>FRUIT PICKUPS</b>\n" +
            "<color=#62E65B>GREEN FRUIT</color>  -  Restore 30 HP\n" +
            "<color=#FFB22E>ORANGE FRUIT</color>  -  Double Jump for 14s\n" +
            "<color=#FF5A55>RED FRUIT</color>  -  Cloud Flight for 10s\n\n" +
            "<b>SCORING</b>\n" +
            "Your score increases while you survive.\n" +
            "The runner speeds up every 30 seconds.\n\n" +
            "<b>COINS & SHOP</b>\n" +
            "Collect coins during runs.\n" +
            "Spend them on starting boosts.\n\n" +
            "<b>TIP</b>\n" +
            "The best fruit often appears on\n" +
            "hard-to-reach elevated platforms.";

        CreateInstructionText(
            "GameplayText",
            leftColumn,
            panel.transform,
            new Vector2(-270f, 10f),
            new Vector2(490f, 500f),
            25,
            TextAnchor.UpperLeft,
            new Color(0.94f, 0.98f, 1f, 1f),
            FontStyle.Normal);

        CreateInstructionText(
            "PickupText",
            rightColumn,
            panel.transform,
            new Vector2(280f, 10f),
            new Vector2(500f, 500f),
            25,
            TextAnchor.UpperLeft,
            new Color(0.94f, 0.98f, 1f, 1f),
            FontStyle.Normal);

        var divider = CreateUIObject("Divider", panel.transform);
        var dividerImage = divider.AddComponent<Image>();
        dividerImage.color = new Color(0.35f, 0.78f, 0.9f, 0.45f);
        var dividerRect = divider.GetComponent<RectTransform>();
        dividerRect.anchorMin = dividerRect.anchorMax = new Vector2(0.5f, 0.5f);
        dividerRect.sizeDelta = new Vector2(3f, 500f);
        dividerRect.anchoredPosition = new Vector2(0f, 5f);

        var closeButton = CreateIconButton("CloseButton", panel.transform, new Vector2(520f, 335f), "X");
        closeButton.onClick.AddListener(CloseHowToPlay);

        var playButton = CreatePanelButton(panel.transform, "START GAME", new Vector2(0f, -320f));
        playButton.onClick.AddListener(StartGame);

        _howToPlayPanel.SetActive(false);
    }

    Text CreateInstructionText(string name, string content, Transform parent, Vector2 position,
        Vector2 size, int fontSize, TextAnchor alignment, Color color, FontStyle style)
    {
        var textGO = CreateUIObject(name, parent);
        var text = textGO.AddComponent<Text>();
        text.text = content;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = color;
        text.supportRichText = true;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;

        var rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return text;
    }

    Button CreateIconButton(string name, Transform parent, Vector2 position, string label)
    {
        var buttonGO = CreateUIObject(name, parent);
        var rect = buttonGO.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(54f, 54f);

        var image = buttonGO.AddComponent<Image>();
        image.color = new Color(0.8f, 0.16f, 0.12f, 1f);

        var button = buttonGO.AddComponent<Button>();
        var colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.28f, 0.2f, 1f);
        colors.pressedColor = new Color(0.58f, 0.08f, 0.06f, 1f);
        button.colors = colors;

        var labelGO = CreateUIObject("Text", buttonGO.transform);
        var text = labelGO.AddComponent<Text>();
        text.text = label;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 28;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        Stretch(text.rectTransform);
        return button;
    }

    Button CreatePanelButton(Transform parent, string label, Vector2 position)
    {
        var buttonGO = CreateUIObject("PanelStartButton", parent);
        var rect = buttonGO.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(360f, 68f);

        var image = buttonGO.AddComponent<Image>();
        image.color = buttonColor;

        var button = buttonGO.AddComponent<Button>();
        var colors = button.colors;
        colors.highlightedColor = buttonHighlightColor;
        colors.pressedColor = buttonPressedColor;
        button.colors = colors;

        var labelGO = CreateUIObject("Text", buttonGO.transform);
        var text = labelGO.AddComponent<Text>();
        text.text = label;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 30;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = new Color(1f, 1f, 0.9f, 1f);
        Stretch(text.rectTransform);
        return button;
    }

    public void OpenHowToPlay()
    {
        if (_howToPlayPanel != null)
            _howToPlayPanel.SetActive(true);
    }

    public void CloseHowToPlay()
    {
        if (_howToPlayPanel != null)
            _howToPlayPanel.SetActive(false);
    }

    void CreateLoadingOverlay(Transform parent)
    {
        var overlayGO = CreateUIObject("LoadingOverlay", parent);
        _loadingOverlay = overlayGO.AddComponent<Image>();
        _loadingOverlay.color = new Color(0f, 0.02f, 0.04f, 0f);
        _loadingOverlay.raycastTarget = false;
        Stretch(_loadingOverlay.rectTransform);
    }

    void AnimateScreen()
    {
        float elapsed = Time.unscaledTime - _createdAt;

        if (_screenGroup != null && !_isLoading)
            _screenGroup.alpha = Mathf.Clamp01(elapsed / 0.45f);

        if (_startButtonRect != null && !_isLoading)
        {
            float pulse = 1f + Mathf.Sin(elapsed * 2.5f) * 0.025f;
            _startButtonRect.localScale = new Vector3(pulse, pulse, 1f);
        }
    }

    public void StartGame()
    {
        if (_isLoading)
            return;

        if (!Application.CanStreamedLevelBeLoaded(sceneToLoad))
        {
            Debug.LogError("TitleScreenUI: scene not found: " + sceneToLoad);
            return;
        }

        StartCoroutine(LoadGame());
    }

    IEnumerator LoadGame()
    {
        _isLoading = true;
        float elapsed = 0f;

        while (elapsed < 0.35f)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / 0.35f);
            if (_loadingOverlay != null)
                _loadingOverlay.color = new Color(0f, 0.02f, 0.04f, t);
            yield return null;
        }

        SceneManager.LoadScene(sceneToLoad);
    }

    public void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    static GameObject CreateUIObject(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}

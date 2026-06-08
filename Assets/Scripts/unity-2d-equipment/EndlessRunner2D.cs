using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Replaces the old static gameplay map with recycled runner segments.
/// Existing tile sprites are harvested before the old Grid is removed.
/// </summary>
[DefaultExecutionOrder(-200)]
public class EndlessRunner2D : MonoBehaviour
{
    const string GameplayScene = "1";

    public enum RunnerMode
    {
        Easy,
        Normal,
        Hard
    }

    [Header("Scroll")]
    public float scrollSpeed = 6.5f;
    public float speedIncreasePerSecond = 0.035f;
    public float maximumSpeed = 11f;

    [Header("Mode")]
    public RunnerMode mode = RunnerMode.Normal;
    [Range(0f, 1f)] public float obstacleChance = 0.38f;
    [Range(0f, 1f)] public float bonusPlatformChance = 0.16f;
    [Range(0f, 1f)] public float gapChance = 0.2f;

    [Header("Track")]
    public float groundTop = -7.1f;
    public float segmentWidth = 8f;
    public int segmentCount = 9;
    public float recycleAtX = -16f;
    public float terrainHeightStep = 0.35f;
    public float terrainMinTop = -7.45f;
    public float terrainMaxTop = -6.25f;
    public int groundFillRows = 4;
    public float mainGroundDepth = 3.8f;

    [Header("Natural Details")]
    [Range(0f, 1f)] public float decorationChance = 0.72f;
    public int maxDecorationsPerSegment = 4;
    public bool createRunnerBackground = true;


    [Header("Legacy Scene Cleanup")]
    public string legacyGridName = "Grid";
    public bool removeLegacyChaseEnemies = true;
    public bool disableLegacyPlayerActions = true;

    [Header("Score")]
    public int scorePerSecond = 10;

    readonly List<Transform> _segments = new List<Transform>();
    readonly List<Sprite> _tileSprites = new List<Sprite>();
    readonly List<Sprite> _surfaceSprites = new List<Sprite>();
    readonly List<Sprite> _fillSprites = new List<Sprite>();
    readonly List<Sprite> _decorationSprites = new List<Sprite>();
    readonly List<Sprite> _obstacleSprites = new List<Sprite>();
    Sprite _skySprite;
    Sprite _forestSprite;
    Transform _player;
    Rigidbody2D _playerBody;
    PlayerAnimatorBridge _playerAnimator;
    Text _hudText;
    int _platformLayer;
    float _nextX;
    float _elapsedSeconds;
    float _currentTerrainTop;
    bool _lastSegmentHadGap;
    int _builtSegments;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateForGameplayScene()
    {
        if (SceneManager.GetActiveScene().name != GameplayScene)
            return;
        if (FindObjectOfType<EndlessRunner2D>() != null)
            return;

        new GameObject("EndlessRunner").AddComponent<EndlessRunner2D>();
    }

    void Awake()
    {
        ApplyModeSettings();
        _platformLayer = LayerMask.NameToLayer(PlayerMovement2D.PlatformLayerName);
        LoadCuratedPaletteSprites();
        HarvestTileSprites();
        EnsurePaletteFallbacks();
        RemoveOldStaticLevel();
        ConfigurePlayerAndCamera();
        CreateBackground();
        CreateHud();

        _currentTerrainTop = groundTop;
        _nextX = -12f;
        for (int i = 0; i < segmentCount; i++)
            CreateSegment();
    }

    void Update()
    {
        _elapsedSeconds += Time.deltaTime;
        UpdateHud();
    }

    void FixedUpdate()
    {
        scrollSpeed = Mathf.Min(maximumSpeed, scrollSpeed + speedIncreasePerSecond * Time.fixedDeltaTime);
        if (_playerAnimator != null)
            _playerAnimator.endlessRunnerVisualSpeed = scrollSpeed;

        foreach (var segment in _segments)
            segment.position += Vector3.left * scrollSpeed * Time.fixedDeltaTime;
        _nextX -= scrollSpeed * Time.fixedDeltaTime;

        if (_playerBody != null)
        {
            _playerBody.position = new Vector2(-3f, _playerBody.position.y);
            _playerBody.velocity = new Vector2(0f, _playerBody.velocity.y);
        }

        Physics2D.SyncTransforms();

        foreach (var segment in _segments)
        {
            if (segment.position.x + segmentWidth * 0.5f >= recycleAtX)
                continue;

            segment.position = new Vector3(_nextX, 0f, 0f);
            _nextX += segmentWidth;
            RebuildSegment(segment);
        }

        if (_player != null && _player.position.y < groundTop - 5f)
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void HarvestTileSprites()
    {
        var seen = new HashSet<Sprite>();
        foreach (var tilemap in FindObjectsOfType<Tilemap>(true))
        {
            if (tilemap.name == PlatformTilemapLayers2D.DefaultWalkableTilemapName)
                continue;

            foreach (var pos in tilemap.cellBounds.allPositionsWithin)
            {
                var sprite = tilemap.GetSprite(pos);
                if (sprite != null && sprite.texture != null &&
                    sprite.texture.name.Contains("InvisibleWalk"))
                    continue;
                if (sprite != null && seen.Add(sprite) && !IsWoodLikeSprite(sprite))
                    _tileSprites.Add(sprite);
                if (_tileSprites.Count >= 24)
                    return;
            }
        }
    }

    void LoadCuratedPaletteSprites()
    {
        var loadedSprites = Resources.FindObjectsOfTypeAll<Sprite>();
        AddNamedSprites(loadedSprites, _surfaceSprites, "Tiles_0", "Tiles_1", "Tiles_3", "Tiles_9", "Tiles_10", "Tiles_25");
        AddNamedSprites(loadedSprites, _fillSprites, "Tiles_2", "Tiles_5", "Tiles_8", "Tiles_17", "Tiles_20", "Tiles_21", "Tiles_22", "Tiles_24");
        AddNamedSprites(loadedSprites, _decorationSprites, "Tiles_11", "Tiles_12", "Tiles_13", "Tiles_14", "Tiles_15", "Tiles_16", "Tiles_23");
        AddNamedSprites(loadedSprites, _obstacleSprites, "Tiles_6", "Tiles_7", "Tiles_18", "Tiles_19");

        foreach (var sprite in loadedSprites)
        {
            if (sprite == null || sprite.texture == null)
                continue;
            if (_skySprite == null && sprite.texture.name == "Background")
                _skySprite = sprite;
            if (_forestSprite == null && sprite.texture.name == "Background 1")
                _forestSprite = sprite;
        }

#if UNITY_EDITOR
        var sprites = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Tiles.png");
        AddNamedSprites(sprites, _surfaceSprites, "Tiles_0", "Tiles_1", "Tiles_3", "Tiles_9", "Tiles_10", "Tiles_25");
        AddNamedSprites(sprites, _fillSprites, "Tiles_2", "Tiles_5", "Tiles_8", "Tiles_17", "Tiles_20", "Tiles_21", "Tiles_22", "Tiles_24");
        AddNamedSprites(sprites, _decorationSprites, "Tiles_11", "Tiles_12", "Tiles_13", "Tiles_14", "Tiles_15", "Tiles_16", "Tiles_23");
        AddNamedSprites(sprites, _obstacleSprites, "Tiles_6", "Tiles_7", "Tiles_18", "Tiles_19");

        _skySprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Background/Background.png");
        _forestSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Background/Background 1.png");
#endif
    }

    static void AddNamedSprites(Object[] assets, List<Sprite> target, params string[] names)
    {
        foreach (string name in names)
        {
            foreach (var asset in assets)
            {
                var sprite = asset as Sprite;
                if (sprite != null && sprite.name == name && !target.Contains(sprite))
                {
                    target.Add(sprite);
                    break;
                }
            }
        }
    }

    void EnsurePaletteFallbacks()
    {
        if (_surfaceSprites.Count == 0)
            AddFallbackSprites(_surfaceSprites, 0, 6);
        if (_fillSprites.Count == 0)
            AddFallbackSprites(_fillSprites, 6, 8);
        if (_decorationSprites.Count == 0)
            AddFallbackSprites(_decorationSprites, 12, 8);
        if (_obstacleSprites.Count == 0)
            AddFallbackSprites(_obstacleSprites, 4, 4);
    }

    void AddFallbackSprites(List<Sprite> target, int start, int count)
    {
        if (_tileSprites.Count == 0)
            return;

        for (int i = 0; i < count; i++)
        {
            var sprite = _tileSprites[(start + i) % _tileSprites.Count];
            if (!target.Contains(sprite))
                target.Add(sprite);
        }
    }

    bool IsWoodLikeSprite(Sprite sprite)
    {
        if (sprite == null)
            return false;
        string name = sprite.name.ToLowerInvariant();
        return name.Contains("wood") || name.Contains("plank");
    }

    void RemoveOldStaticLevel()
    {
        var activeScene = SceneManager.GetActiveScene();
        var grid = GameObject.Find(legacyGridName);
        if (grid != null && grid.scene == activeScene && grid.GetComponent<Grid>() != null)
        {
            grid.SetActive(false);
            Destroy(grid);
        }

        if (!removeLegacyChaseEnemies)
            return;

        foreach (var enemy in FindObjectsOfType<ChaseEnemy2D>(true))
        {
            if (enemy == null || enemy.gameObject.scene != activeScene)
                continue;
            if (enemy.GetComponentInParent<EndlessRunner2D>() != null)
                continue;

            enemy.gameObject.SetActive(false);
            Destroy(enemy.gameObject);
        }
    }

    void ConfigurePlayerAndCamera()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return;

        _player = player.transform;
        _playerBody = player.GetComponent<Rigidbody2D>();
        _player.position = new Vector3(-3f, groundTop + 0.65f, 0f);

        var movement = player.GetComponent<PlayerMovement2D>();
        if (movement != null)
            movement.endlessRunnerMode = true;

        if (disableLegacyPlayerActions)
        {
            var combat = player.GetComponent<PlayerEquipmentCombat>();
            if (combat != null)
                combat.enabled = false;
            var dodge = player.GetComponent<PlayerDodge2D>();
            if (dodge != null)
                dodge.enabled = false;
        }

        _playerAnimator = player.GetComponent<PlayerAnimatorBridge>();
        if (_playerAnimator != null)
        {
            _playerAnimator.endlessRunnerMode = true;
            _playerAnimator.endlessRunnerVisualSpeed = scrollSpeed;
        }

        var camera = Camera.main;
        if (camera == null)
            return;

        var follow = camera.GetComponent<CameraFollow2D>();
        if (follow != null)
            follow.lockHorizontal = true;

        camera.backgroundColor = new Color(0.58f, 0.82f, 0.84f);
    }

    void CreateBackground()
    {
        if (!createRunnerBackground || GameObject.Find("RunnerBackground") != null)
            return;

        var camera = Camera.main;
        if (camera == null)
            return;

        var root = new GameObject("RunnerBackground");
        root.transform.SetParent(camera.transform, false);
        root.transform.localPosition = new Vector3(0f, 0f, 10f);

        if (_skySprite != null)
            CreateBackgroundLayer(root.transform, _skySprite, "Sky", new Vector3(0f, 0.4f, 0f), 5.9f, -100);
        if (_forestSprite != null)
            CreateBackgroundLayer(root.transform, _forestSprite, "Forest", new Vector3(0f, -1.65f, 0.1f), 3.25f, -90);
    }

    void CreateBackgroundLayer(Transform parent, Sprite sprite, string layerName, Vector3 localPosition, float scale, int sortingOrder)
    {
        var layer = new GameObject(layerName);
        layer.transform.SetParent(parent, false);
        layer.transform.localPosition = localPosition;
        layer.transform.localScale = new Vector3(scale, scale, 1f);

        var renderer = layer.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = sortingOrder;
    }

    void CreateSegment()
    {
        var segment = new GameObject("RunnerSegment_" + _builtSegments).transform;
        segment.SetParent(transform, false);
        segment.position = new Vector3(_nextX, 0f, 0f);
        _nextX += segmentWidth;
        _segments.Add(segment);
        RebuildSegment(segment);
    }

    void RebuildSegment(Transform segment)
    {
        for (int i = segment.childCount - 1; i >= 0; i--)
        {
            segment.GetChild(i).gameObject.SetActive(false);
            Destroy(segment.GetChild(i).gameObject);
        }

        int index = _builtSegments++;
        float top = NextTerrainTop(index);
        bool canMakeGap = index > 4 && !_lastSegmentHadGap && Random.value < gapChance;


        if (canMakeGap)
            CreateGapSegment(segment, top);
        else
            CreatePlatform(segment, Vector2.zero, segmentWidth, top);

        if (canMakeGap)
            CreateGapSegment(segment, top);
        else
            CreatePlatform(segment, Vector2.zero, segmentWidth, top);

        if (!canMakeGap && index > 1 && Random.value < obstacleChance)
        {
            float obstacleX = Random.Range(-segmentWidth * 0.2f, segmentWidth * 0.3f);
            CreateObstacle(segment, obstacleX, top);
        }

        if (index > 2 && Random.value < bonusPlatformChance)
        {
            float width = Random.Range(2f, 3.5f);
            float x = Random.Range(-segmentWidth * 0.25f, segmentWidth * 0.25f);
            CreateFloatingPlatform(segment, x, width, top + Random.Range(2.1f, 2.8f));

        }

        if (Random.value < decorationChance)
            CreateDecorations(segment, top, canMakeGap);

        _lastSegmentHadGap = canMakeGap;
    }

    float NextTerrainTop(int index)
    {
        if (index < 4)
            return groundTop;

        float step = Random.Range(-terrainHeightStep, terrainHeightStep);
        if (Random.value < 0.35f)
            step = 0f;

        _currentTerrainTop = Mathf.Clamp(_currentTerrainTop + step, terrainMinTop, terrainMaxTop);
        return _currentTerrainTop;
    }

    void CreateGapSegment(Transform segment, float top)
    {
        float gapWidth = Random.Range(1.7f, 2.35f);
        float gapCenter = Random.Range(-0.8f, 0.8f);
        float leftEdge = -segmentWidth * 0.5f;
        float rightEdge = segmentWidth * 0.5f;
        float gapLeft = gapCenter - gapWidth * 0.5f;
        float gapRight = gapCenter + gapWidth * 0.5f;

        float leftWidth = Mathf.Max(1.25f, gapLeft - leftEdge);
        float rightWidth = Mathf.Max(1.25f, rightEdge - gapRight);
        CreatePlatform(segment, new Vector2(leftEdge + leftWidth * 0.5f, 0f), leftWidth, top);
        CreatePlatform(segment, new Vector2(gapRight + rightWidth * 0.5f, 0f), rightWidth, top);

        CreateDropDecoration(segment, gapLeft, top);
        CreateDropDecoration(segment, gapRight, top);
    }

    void ApplyModeSettings()
    {
        switch (mode)
        {
            case RunnerMode.Easy:
                scrollSpeed = Mathf.Min(scrollSpeed, 5.8f);
                speedIncreasePerSecond = 0.02f;
                maximumSpeed = 8.5f;
                obstacleChance = 0.4f;
                bonusPlatformChance = 0.45f;
                gapChance = 0.14f;
                decorationChance = 0.84f;
                break;
            case RunnerMode.Hard:
                scrollSpeed = Mathf.Max(scrollSpeed, 7.3f);
                speedIncreasePerSecond = 0.055f;
                maximumSpeed = 13f;
                obstacleChance = 0.72f;
                bonusPlatformChance = 0.26f;
                gapChance = 0.32f;
                decorationChance = 0.62f;
                break;
        }

        if (Random.value < decorationChance)
            CreateDecorations(segment, top, canMakeGap);

        _lastSegmentHadGap = canMakeGap;
    }

    float NextTerrainTop(int index)
    {
        if (index < 4)
            return groundTop;

        float step = Random.Range(-terrainHeightStep, terrainHeightStep);
        if (Random.value < 0.35f)
            step = 0f;

        _currentTerrainTop = Mathf.Clamp(_currentTerrainTop + step, terrainMinTop, terrainMaxTop);
        return _currentTerrainTop;
    }

    void CreateGapSegment(Transform segment, float top)
    {
        float gapWidth = Random.Range(1.7f, 2.35f);
        float gapCenter = Random.Range(-0.8f, 0.8f);
        float leftEdge = -segmentWidth * 0.5f;
        float rightEdge = segmentWidth * 0.5f;
        float gapLeft = gapCenter - gapWidth * 0.5f;
        float gapRight = gapCenter + gapWidth * 0.5f;

        float leftWidth = Mathf.Max(1.25f, gapLeft - leftEdge);
        float rightWidth = Mathf.Max(1.25f, rightEdge - gapRight);
        CreatePlatform(segment, new Vector2(leftEdge + leftWidth * 0.5f, 0f), leftWidth, top);
        CreatePlatform(segment, new Vector2(gapRight + rightWidth * 0.5f, 0f), rightWidth, top);

        CreateDropDecoration(segment, gapLeft, top);
        CreateDropDecoration(segment, gapRight, top);
    }

    void ApplyModeSettings()
    {
        switch (mode)
        {
            case RunnerMode.Easy:
                scrollSpeed = Mathf.Min(scrollSpeed, 5.8f);
                speedIncreasePerSecond = 0.02f;
                maximumSpeed = 8.5f;
                obstacleChance = 0.4f;
                bonusPlatformChance = 0.45f;
                gapChance = 0.14f;
                decorationChance = 0.84f;
                break;
            case RunnerMode.Hard:
                scrollSpeed = Mathf.Max(scrollSpeed, 7.3f);
                speedIncreasePerSecond = 0.055f;
                maximumSpeed = 13f;
                obstacleChance = 0.72f;
                bonusPlatformChance = 0.26f;
                gapChance = 0.32f;
                decorationChance = 0.62f;
                break;
        }
    }

    void CreateHud()
    {
        if (GameObject.Find("RunnerHud") != null)
            return;

        var canvasGO = new GameObject("RunnerHud");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        var textGO = new GameObject("TimerScoreText");
        textGO.transform.SetParent(canvasGO.transform, false);
        _hudText = textGO.AddComponent<Text>();
        _hudText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _hudText.fontSize = 34;
        _hudText.alignment = TextAnchor.UpperRight;
        _hudText.color = Color.white;
        _hudText.horizontalOverflow = HorizontalWrapMode.Overflow;
        _hudText.verticalOverflow = VerticalWrapMode.Overflow;

        var rect = _hudText.rectTransform;
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-36f, -28f);
        rect.sizeDelta = new Vector2(420f, 120f);

        UpdateHud();
    }

    void UpdateHud()
    {
        if (_hudText == null)
            return;

        int totalSeconds = Mathf.FloorToInt(_elapsedSeconds);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        int score = Mathf.FloorToInt(_elapsedSeconds * scorePerSecond);
        _hudText.text = string.Format("TIME {0:00}:{1:00}\nSCORE {2:000000}", minutes, seconds, score);
    }

    void CreateHud()
    {
        if (GameObject.Find("RunnerHud") != null)
            return;

        var canvasGO = new GameObject("RunnerHud");
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        var textGO = new GameObject("TimerScoreText");
        textGO.transform.SetParent(canvasGO.transform, false);
        _hudText = textGO.AddComponent<Text>();
        _hudText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _hudText.fontSize = 34;
        _hudText.alignment = TextAnchor.UpperRight;
        _hudText.color = Color.white;
        _hudText.horizontalOverflow = HorizontalWrapMode.Overflow;
        _hudText.verticalOverflow = VerticalWrapMode.Overflow;

        var rect = _hudText.rectTransform;
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-36f, -28f);
        rect.sizeDelta = new Vector2(420f, 120f);

        UpdateHud();
    }

    void UpdateHud()
    {
        if (_hudText == null)
            return;

        int totalSeconds = Mathf.FloorToInt(_elapsedSeconds);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        int score = Mathf.FloorToInt(_elapsedSeconds * scorePerSecond);
        _hudText.text = string.Format("TIME {0:00}:{1:00}\nSCORE {2:000000}", minutes, seconds, score);
    }

    void CreatePlatform(Transform segment, Vector2 localOffset, float width, float top, float visualDepth = -1f)
    {
        var platform = new GameObject("Platform");
        platform.layer = _platformLayer;
        platform.transform.SetParent(segment, false);
        platform.transform.localPosition = new Vector3(localOffset.x, top - 0.5f, 0f);

        var collider = platform.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(width, 1f);

        float depth = visualDepth > 0f ? visualDepth : Mathf.Lerp(1.25f, mainGroundDepth, Mathf.InverseLerp(2.4f, segmentWidth, width));
        var surface = PickSprite(_surfaceSprites, _builtSegments + Mathf.RoundToInt(width * 10f));
        if (surface != null)
            CreateSpriteVisual(platform.transform, surface, new Vector3(0f, 0.58f - depth * 0.5f, 0f), width, depth, 0);

        if (width > 2.8f)
        {
            var fill = PickSprite(_fillSprites, _builtSegments + Mathf.RoundToInt(localOffset.x * 10f));
            if (fill != null)
                CreateSpriteVisual(platform.transform, fill, new Vector3(0f, -0.1f - depth * 0.5f, 0f), width * 0.96f, depth * 0.82f, -1);
        }
    }

    void CreateFloatingPlatform(Transform segment, float localX, float width, float top)
    {
        CreatePlatform(segment, new Vector2(localX, 0f), width, top, 1.05f);

        if (Random.value < 0.45f)
        {
            var stone = PickSprite(_fillSprites, _builtSegments + 5);
            if (stone != null)
                CreateSpriteVisual(segment, stone, new Vector3(localX + Random.Range(-width * 0.2f, width * 0.2f), top - 1.35f, 0f),
                    width * Random.Range(0.35f, 0.55f), Random.Range(0.75f, 1.15f), -2);
        }
    }

    void CreateObstacle(Transform segment, float localX, float top)
    {
        var obstacle = new GameObject("Obstacle");
        obstacle.transform.SetParent(segment, false);
        obstacle.transform.localPosition = new Vector3(localX, top + 0.58f, 0f);
        obstacle.AddComponent<EndlessRunnerObstacle2D>();

        var collider = obstacle.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(0.8f, 1.15f);

        var obstacleSprite = PickSprite(_obstacleSprites, _builtSegments + 3);
        if (obstacleSprite != null)
            CreateSpriteVisual(obstacle.transform, obstacleSprite, Vector3.zero, 0.95f, 1.1f, 2);
    }

    void CreateDecorations(Transform segment, float top, bool hasGap)
    {
        int count = Random.Range(1, Mathf.Max(2, maxDecorationsPerSegment + 1));
        for (int i = 0; i < count; i++)
        {
            float x = Random.Range(-segmentWidth * 0.42f, segmentWidth * 0.42f);
            if (hasGap && Mathf.Abs(x) < 1.3f)
                x += Mathf.Sign(x == 0f ? 1f : x) * 1.45f;

            float size = Random.Range(0.28f, 0.52f);
            float y = top + Random.Range(0.12f, 0.2f);
            var decoration = PickSprite(_decorationSprites, _builtSegments + i * 7 + Random.Range(0, 8));
            var visual = decoration == null ? null : CreateSpriteVisual(segment, decoration, new Vector3(x, y, 0f), size, size * Random.Range(1.1f, 1.8f), 2);
            if (visual != null)
            {
                visual.name = "GroundDecoration";
                visual.transform.localScale = new Vector3(
                    visual.transform.localScale.x * Random.Range(0.75f, 1.35f),
                    visual.transform.localScale.y * Random.Range(0.6f, 1.1f),
                    1f);
            }
        }
    }

    void CreateDropDecoration(Transform segment, float x, float top)
    {
        for (int i = 0; i < 3; i++)
        {
            float y = top - 0.75f - i * 0.65f;
            var stone = PickSprite(_fillSprites, _builtSegments + i * 4);
            if (stone != null)
                CreateSpriteVisual(segment, stone, new Vector3(x + Random.Range(-0.18f, 0.18f), y, 0f),
                    Random.Range(0.55f, 0.9f), Random.Range(0.45f, 0.8f), -1);
        }
    }

    Sprite PickSprite(List<Sprite> sprites, int index)
    {
        if (sprites == null || sprites.Count == 0)
            return null;

        return sprites[Mathf.Abs(index) % sprites.Count];
    }

    GameObject CreateSpriteVisual(Transform parent, Sprite sprite, Vector3 localPosition, float targetWidth, float targetHeight, int sortingOrder)
    {
        if (sprite == null)
            return null;

        var visual = new GameObject("TileVisual");
        visual.transform.SetParent(parent, false);
        visual.transform.localPosition = localPosition;

        var renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = sortingOrder;

        Vector2 spriteSize = renderer.sprite.bounds.size;
        visual.transform.localScale = new Vector3(
            targetWidth / Mathf.Max(0.01f, spriteSize.x),
            targetHeight / Mathf.Max(0.01f, spriteSize.y),
            1f);
        return visual;
    }
}

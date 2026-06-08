using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

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
    [Range(0f, 1f)] public float obstacleChance = 0.62f;
    [Range(0f, 1f)] public float bonusPlatformChance = 0.38f;
    [Range(0f, 1f)] public float gapChance = 0.24f;

    [Header("Track")]
    public float groundTop = -7.1f;
    public float segmentWidth = 8f;
    public int segmentCount = 9;
    public float recycleAtX = -16f;
    public float terrainHeightStep = 0.35f;
    public float terrainMinTop = -7.45f;
    public float terrainMaxTop = -6.25f;
    public int groundFillRows = 4;

    [Header("Natural Details")]
    [Range(0f, 1f)] public float decorationChance = 0.72f;
    public int maxDecorationsPerSegment = 4;


    [Header("Legacy Scene Cleanup")]
    public string legacyGridName = "Grid";
    public bool removeLegacyChaseEnemies = true;

    [Header("Score")]
    public int scorePerSecond = 10;

    readonly List<Transform> _segments = new List<Transform>();
    readonly List<Sprite> _tileSprites = new List<Sprite>();
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
        HarvestTileSprites();
        RemoveOldStaticLevel();
        ConfigurePlayerAndCamera();
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
                if (sprite != null && seen.Add(sprite))
                    _tileSprites.Add(sprite);
                if (_tileSprites.Count >= 24)
                    return;
            }
        }
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

    void CreatePlatform(Transform segment, Vector2 localOffset, float width, float top)
    {
        var platform = new GameObject("Platform");
        platform.layer = _platformLayer;
        platform.transform.SetParent(segment, false);
        platform.transform.localPosition = new Vector3(localOffset.x, top - 0.5f, 0f);

        var collider = platform.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(width, 1f);

        int tileCount = Mathf.CeilToInt(width);
        for (int i = 0; i < tileCount; i++)
        {
            float x = -width * 0.5f + 0.5f + i;
            CreateTileVisual(platform.transform, new Vector3(x, 0.52f, 0f), i, 1.05f, 0);

            int fillRows = Mathf.Max(1, groundFillRows);
            for (int row = 1; row <= fillRows; row++)
            {
                float fillX = x + Random.Range(-0.04f, 0.04f);
                float fillY = 0.52f - row;
                int spriteIndex = i + row * 5 + _builtSegments;
                float size = Random.Range(0.94f, 1.08f);
                CreateTileVisual(platform.transform, new Vector3(fillX, fillY, 0f), spriteIndex, size, -row);
            }
        }
    }

    void CreateFloatingPlatform(Transform segment, float localX, float width, float top)
    {
        CreatePlatform(segment, new Vector2(localX, 0f), width, top);

        int supports = Random.Range(1, 3);
        for (int i = 0; i < supports; i++)
        {
            float supportX = localX + Random.Range(-width * 0.35f, width * 0.35f);
            int stack = Random.Range(1, 3);
            for (int row = 0; row < stack; row++)
            {
                CreateTileVisual(segment, new Vector3(supportX, top - 1.25f - row * 0.75f, 0f),
                    _builtSegments + row + i * 3, Random.Range(0.55f, 0.78f), -2);
            }
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

        CreateTileVisual(obstacle.transform, new Vector3(-0.12f, -0.05f, 0f), _builtSegments + 3, 0.82f, 1);
        CreateTileVisual(obstacle.transform, new Vector3(0.16f, 0.2f, 0f), _builtSegments + 8, 0.62f, 2);
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
            int spriteIndex = _builtSegments + i * 7 + Random.Range(0, 8);
            var visual = CreateTileVisual(segment, new Vector3(x, y, 0f), spriteIndex, size, 2);
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
            float size = Random.Range(0.45f, 0.78f);
            CreateTileVisual(segment, new Vector3(x + Random.Range(-0.18f, 0.18f), y, 0f),
                _builtSegments + i * 4, size, -1);
        }
    }

    GameObject CreateTileVisual(Transform parent, Vector3 localPosition, int spriteIndex, float targetSize = 1.05f, int sortingOrder = 0)
    {
        if (_tileSprites.Count == 0)
            return null;

        var visual = new GameObject("TileVisual");
        visual.transform.SetParent(parent, false);
        visual.transform.localPosition = localPosition;

        var renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = _tileSprites[Mathf.Abs(spriteIndex) % _tileSprites.Count];
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = sortingOrder;

        Vector2 spriteSize = renderer.sprite.bounds.size;
        float scale = targetSize / Mathf.Max(0.01f, Mathf.Max(spriteSize.x, spriteSize.y));
        visual.transform.localScale = new Vector3(scale, scale, 1f);
        return visual;
    }
}

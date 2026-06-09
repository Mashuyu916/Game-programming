using System.Collections;
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
    static EndlessRunner2D _activeRunner;

    public enum RunnerMode
    {
        Easy,
        Normal,
        Hard
    }

    [Header("Scroll")]
    public float scrollSpeed = 6.5f;
    public float speedIncreasePerSecond = 0.008f;
    public float speedIncreasePerMinute = 0.45f;
    public float maximumSpeed = 11f;

    [Header("Mode")]
    public RunnerMode mode = RunnerMode.Normal;
    [Range(0f, 1f)] public float obstacleChance = 0.24f;
    [Range(0f, 1f)] public float bonusPlatformChance = 0.08f;
    [Range(0f, 1f)] public float gapChance = 0.28f;
    [Range(0f, 1f)] public float challengeSegmentChance = 0.42f;

    [Header("Track")]
    public float groundTop = -7.1f;
    public float segmentWidth = 8f;
    public int segmentCount = 9;
    public float recycleAtX = -16f;
    public float terrainHeightStep = 0.35f;
    public float terrainMinTop = -7.45f;
    public float terrainMaxTop = -6.25f;
    public int groundFillRows = 4;
    public float mainGroundDepth = 4f;
    public Color terrainFillColor = new Color(0.54f, 0.82f, 0.82f, 0f);
    public float playerGroundSkin = 0.02f;
    public float surfaceTileOverlap = 0.12f;

    [Header("Natural Details")]
    [Range(0f, 1f)] public float decorationChance = 0.42f;
    public int maxDecorationsPerSegment = 2;
    public bool createRunnerBackground = true;

    [Header("Death Feedback")]
    public float deathFeedbackSeconds = 0.85f;
    public Color deathEffectColor = new Color(1f, 0.24f, 0.16f, 0.9f);
    public Color deathReasonColor = new Color(1f, 0.96f, 0.82f, 1f);

    [Header("Fruit Pickups")]
    [Range(0f, 1f)] public float highPlatformPickupChance = 0.72f;
    [Range(0f, 1f)] public float healFruitWeight = 0.58f;
    [Range(0f, 1f)] public float doubleJumpFruitWeight = 0.25f;
    public float healFruitAmount = 30f;
    public float doubleJumpDuration = 14f;
    public float rollDuration = 12f;

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
    readonly List<Sprite> _fruitSprites = new List<Sprite>();
    Sprite _skySprite;
    Sprite _solidSprite;
    Transform _player;
    Rigidbody2D _playerBody;
    PlayerAnimatorBridge _playerAnimator;
    PlayerHealthReload _playerHealth;
    Text _hudText;
    Image _healthFillImage;
    Text _healthText;
    Text _damageText;
    Text _pickupText;
    Coroutine _damageTextRoutine;
    Coroutine _pickupTextRoutine;
    int _platformLayer;
    float _nextX;
    float _elapsedSeconds;
    float _currentTerrainTop;
    float _startingScrollSpeed;
    int _speedMinuteLevel;
    bool _lastSegmentHadGap;
    bool _isRestarting;
    int _challengeCooldown;
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
        _activeRunner = this;
        ApplyModeSettings();
        _startingScrollSpeed = scrollSpeed;
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

    void OnDestroy()
    {
        if (_playerHealth != null)
        {
            _playerHealth.Damaged -= OnPlayerDamaged;
            _playerHealth.Healed -= OnPlayerHealed;
        }
        if (_activeRunner == this)
            _activeRunner = null;
    }

    public static bool TryRestartActiveRunner()
    {
        return TryRestartActiveRunner("DEATH", null);
    }

    public static bool TryRestartActiveRunner(string reason, GameObject source)
    {
        if (_activeRunner == null)
            return false;

        Vector3 position = _activeRunner._player != null ? _activeRunner._player.position : Vector3.zero;
        _activeRunner.BeginDeathRestart(reason, position, source);
        return true;
    }

    public static void TryShowPickupMessage(string message)
    {
        if (_activeRunner != null)
            _activeRunner.ShowPickupMessage(message);
    }

    void Update()
    {
        _elapsedSeconds += Time.deltaTime;
        UpdateHud();
    }

    void FixedUpdate()
    {
        if (_isRestarting)
            return;

        scrollSpeed = Mathf.Min(maximumSpeed, scrollSpeed + speedIncreasePerSecond * Time.fixedDeltaTime);
        int elapsedMinutes = Mathf.FloorToInt(_elapsedSeconds / 60f);
        if (elapsedMinutes > _speedMinuteLevel)
        {
            int gainedLevels = elapsedMinutes - _speedMinuteLevel;
            _speedMinuteLevel = elapsedMinutes;
            scrollSpeed = Mathf.Min(maximumSpeed, scrollSpeed + speedIncreasePerMinute * gainedLevels);
            ShowPickupMessage("SPEED UP  LEVEL " + (_speedMinuteLevel + 1));
        }
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
            BeginDeathRestart("FELL INTO A GAP", _player.position, null);
    }

    void RestartRunner()
    {
        _isRestarting = false;
        scrollSpeed = _startingScrollSpeed;
        _elapsedSeconds = 0f;
        _builtSegments = 0;
        _lastSegmentHadGap = false;
        _challengeCooldown = 0;
        _speedMinuteLevel = 0;
        _currentTerrainTop = groundTop;

        for (int i = _segments.Count - 1; i >= 0; i--)
        {
            if (_segments[i] != null)
            {
                _segments[i].gameObject.SetActive(false);
                Destroy(_segments[i].gameObject);
            }
        }
        _segments.Clear();

        ConfigurePlayerAndCamera();
        if (_playerBody != null)
        {
            PlacePlayerAtGroundTop(groundTop);
            _playerBody.velocity = Vector2.zero;
            _playerBody.angularVelocity = 0f;
        }
        else if (_player != null)
        {
            PlacePlayerAtGroundTop(groundTop);
        }

        _nextX = -12f;
        for (int i = 0; i < segmentCount; i++)
            CreateSegment();

        if (_playerHealth != null)
            _playerHealth.RestoreFullHealth();
        ResetPickupAbilities();
        UpdateHud();
    }

    void ResetPickupAbilities()
    {
        if (_player == null)
            return;

        var movement = _player.GetComponent<PlayerMovement2D>();
        if (movement != null)
            movement.ClearDoubleJumpAbility();

        var dodge = _player.GetComponent<PlayerDodge2D>();
        if (dodge != null)
            dodge.ClearRollAbility();
    }

    void BeginDeathRestart(string reason, Vector3 worldPosition, GameObject source)
    {
        if (_isRestarting)
            return;

        _isRestarting = true;
        if (_playerBody != null)
            _playerBody.velocity = Vector2.zero;

        StartCoroutine(DeathRestartRoutine(FormatDeathReason(reason, source), worldPosition));
    }

    IEnumerator DeathRestartRoutine(string reason, Vector3 worldPosition)
    {
        CreateDeathFeedback(worldPosition, reason);
        yield return new WaitForSeconds(deathFeedbackSeconds);
        RestartRunner();
    }

    string FormatDeathReason(string reason, GameObject source)
    {
        if (!string.IsNullOrEmpty(reason))
            return reason;
        if (source != null && source.GetComponent<EndlessRunnerObstacle2D>() != null)
            return "HIT AN OBSTACLE";
        return "DEATH";
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
        CreateSolidSprite();
        Texture2D tilesTexture = null;
        Texture2D platformTexture = null;
        Texture2D fruitTexture = null;
        var loadedSprites = Resources.FindObjectsOfTypeAll<Sprite>();
        foreach (var sprite in loadedSprites)
        {
            if (sprite != null && sprite.texture != null && sprite.texture.name == "Tiles")
            {
                tilesTexture = sprite.texture;
                continue;
            }
            if (sprite != null && sprite.texture != null && sprite.texture.name == "platforms")
                platformTexture = sprite.texture;
            if (sprite != null && sprite.texture != null && sprite.texture.name == "fruit")
                fruitTexture = sprite.texture;
        }

        AddNamedSprites(loadedSprites, _decorationSprites, "Tiles_11", "Tiles_12", "Tiles_13", "Tiles_14", "Tiles_15", "Tiles_16", "Tiles_23");
        AddNamedSprites(loadedSprites, _obstacleSprites, "Tiles_6", "Tiles_7", "Tiles_18");

        foreach (var sprite in loadedSprites)
        {
            if (sprite == null || sprite.texture == null)
                continue;
            if (_skySprite == null && sprite.texture.name == "Background")
                _skySprite = sprite;
        }

#if UNITY_EDITOR
        if (platformTexture == null)
        {
            const string platformTexturePath = "Assets/Art/RunnerTiles/brackeys_platformer_assets/sprites/platforms.png";
            AssetDatabase.ImportAsset(platformTexturePath);
            platformTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                platformTexturePath);
        }
        if (fruitTexture == null)
        {
            const string fruitTexturePath = "Assets/Art/RunnerTiles/brackeys_platformer_assets/sprites/fruit.png";
            AssetDatabase.ImportAsset(fruitTexturePath);
            fruitTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(fruitTexturePath);
        }
        if (tilesTexture == null)
            tilesTexture = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Tiles.png");

        var sprites = AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Tiles.png");
        AddNamedSprites(sprites, _decorationSprites, "Tiles_11", "Tiles_12", "Tiles_13", "Tiles_14", "Tiles_15", "Tiles_16", "Tiles_23");
        AddNamedSprites(sprites, _obstacleSprites, "Tiles_6", "Tiles_7", "Tiles_18");

        _skySprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Background/Background.png");
#endif

        if (platformTexture != null)
            BuildPlatformPalette(platformTexture);
        else if (tilesTexture != null)
            BuildSlicedTerrainPalette(tilesTexture);
        if (fruitTexture != null)
            BuildFruitPalette(fruitTexture);
    }

    void CreateSolidSprite()
    {
        if (_solidSprite != null)
            return;

        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        _solidSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
    }

    void BuildSlicedTerrainPalette(Texture2D texture)
    {
        _surfaceSprites.Clear();
        _fillSprites.Clear();

        AddSlices(texture, _surfaceSprites, 0, 480, 32, 32, 6);
        AddSlices(texture, _surfaceSprites, 0, 288, 32, 32, 6);
        AddSlices(texture, _fillSprites, 0, 416, 32, 32, 6);
        AddSlices(texture, _fillSprites, 0, 384, 32, 32, 6);
        AddSlices(texture, _fillSprites, 224, 96, 32, 32, 3);
    }

    void BuildPlatformPalette(Texture2D texture)
    {
        _surfaceSprites.Clear();
        _fillSprites.Clear();

        int tileSize = 16;
        AddSlices(texture, _surfaceSprites, 0, texture.height - tileSize, tileSize, tileSize, 4);
        AddSlices(texture, _fillSprites, 0, texture.height - tileSize * 2, tileSize, tileSize, 4);
        AddSlices(texture, _fillSprites, 0, texture.height - tileSize * 3, tileSize, tileSize, 4);
    }

    void BuildFruitPalette(Texture2D texture)
    {
        _fruitSprites.Clear();

        int tileSize = 16;
        AddSlices(texture, _fruitSprites, 0, texture.height - tileSize, tileSize, tileSize, 3);
    }

    void AddSlices(Texture2D texture, List<Sprite> target, int startX, int y, int width, int height, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var rect = new Rect(startX + width * i, y, width, height);
            target.Add(Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 32f));
        }
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
        PlacePlayerAtGroundTop(groundTop);
        AlignPlayerVisualToCollider();

        if (_playerHealth != null)
        {
            _playerHealth.Damaged -= OnPlayerDamaged;
            _playerHealth.Healed -= OnPlayerHealed;
        }
        _playerHealth = player.GetComponent<PlayerHealthReload>();
        if (_playerHealth != null)
        {
            _playerHealth.Damaged += OnPlayerDamaged;
            _playerHealth.Healed += OnPlayerHealed;
        }

        var movement = player.GetComponent<PlayerMovement2D>();
        if (movement != null)
            movement.endlessRunnerMode = true;

        var dodge = player.GetComponent<PlayerDodge2D>();
        if (dodge != null)
        {
            dodge.enabled = true;
            dodge.endlessRunnerMode = true;
        }

        if (disableLegacyPlayerActions)
        {
            var combat = player.GetComponent<PlayerEquipmentCombat>();
            if (combat != null)
                combat.enabled = false;
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

    void PlacePlayerAtGroundTop(float top)
    {
        if (_player == null)
            return;

        _player.position = new Vector3(-3f, top + 2f, 0f);
        Physics2D.SyncTransforms();

        var collider = _player.GetComponent<Collider2D>();
        if (collider == null)
        {
            _player.position = new Vector3(-3f, top + 0.5f + playerGroundSkin, 0f);
            return;
        }

        float yOffset = top + playerGroundSkin - collider.bounds.min.y;
        _player.position += new Vector3(0f, yOffset, 0f);

        if (_playerBody != null)
            _playerBody.position = _player.position;
    }

    void AlignPlayerVisualToCollider()
    {
        if (_player == null)
            return;

        var collider = _player.GetComponent<Collider2D>();
        var renderer = _player.GetComponentInChildren<SpriteRenderer>();
        if (collider == null || renderer == null)
            return;

        Physics2D.SyncTransforms();
        float delta = collider.bounds.min.y - renderer.bounds.min.y;
        renderer.transform.position += new Vector3(0f, delta, 0f);
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
            CreateBackgroundLayer(root.transform, _skySprite, "Sky", new Vector3(0f, 0.2f, 0f), 5.9f, -100);
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
        if (_challengeCooldown > 0)
            _challengeCooldown--;

        bool canMakeGap = index > 4 && !_lastSegmentHadGap && Random.value < gapChance;
        bool canMakeChallenge = index > 5 && _challengeCooldown == 0 && !_lastSegmentHadGap && !canMakeGap && Random.value < challengeSegmentChance;

        if (canMakeChallenge)
        {
            CreateChallengeSegment(segment, top, index);
            _challengeCooldown = 2;
        }
        else if (canMakeGap)
            CreateGapSegment(segment, top);
        else
            CreatePlatform(segment, Vector2.zero, segmentWidth, top, -1f, false);

        if (!canMakeGap && !canMakeChallenge && index > 1 && Random.value < obstacleChance)
        {
            float obstacleX = Random.Range(-segmentWidth * 0.2f, segmentWidth * 0.3f);
            CreateObstacle(segment, obstacleX, top);
        }

        if (!canMakeChallenge && index > 2 && Random.value < bonusPlatformChance)
        {
            float width = Random.Range(2f, 3.5f);
            float x = Random.Range(-segmentWidth * 0.25f, segmentWidth * 0.25f);
            CreateFloatingPlatform(segment, x, width, top + Random.Range(2.1f, 2.8f));
        }

        if (Random.value < decorationChance)
            CreateDecorations(segment, top, canMakeGap);

        _lastSegmentHadGap = canMakeGap;
    }

    void CreateChallengeSegment(Transform segment, float top, int index)
    {
        int pattern = index % 5;
        if (pattern == 0)
        {
            CreatePlatform(segment, new Vector2(-3.25f, 0f), 1.9f, top);
            CreatePlatform(segment, new Vector2(0.35f, 0f), 1.45f, top + 0.9f, 1.2f);
            CreatePlatform(segment, new Vector2(3.45f, 0f), 1.65f, top + 0.9f, 1.2f);
            MaybeCreateFruitPickup(segment, 0.35f, top + 0.9f);
        }
        else if (pattern == 1)
        {
            CreatePlatform(segment, new Vector2(-3.35f, 0f), 1.75f, top);
            CreatePlatform(segment, new Vector2(-0.05f, 0f), 1.2f, top + 1.1f, 1.1f);
            CreatePlatform(segment, new Vector2(3.35f, 0f), 1.75f, top + 0.25f, 1.45f);
            MaybeCreateFruitPickup(segment, -0.05f, top + 1.1f);
        }
        else if (pattern == 2)
        {
            CreatePlatform(segment, new Vector2(-3.35f, 0f), 1.8f, top + 0.45f, 1.35f);
            CreatePlatform(segment, new Vector2(-0.25f, 0f), 1.05f, top + 1.2f, 1.05f);
            CreatePlatform(segment, new Vector2(3.25f, 0f), 1.9f, top, 1.55f);
            MaybeCreateFruitPickup(segment, -0.25f, top + 1.2f);
        }
        else if (pattern == 3)
        {
            CreatePlatform(segment, new Vector2(-3.45f, 0f), 1.55f, top);
            CreatePlatform(segment, new Vector2(-0.75f, 0f), 1.0f, top + 0.75f, 1.05f);
            CreatePlatform(segment, new Vector2(1.55f, 0f), 1.0f, top + 1.25f, 1.05f);
            CreatePlatform(segment, new Vector2(3.55f, 0f), 1.25f, top + 0.55f, 1.1f);
            MaybeCreateFruitPickup(segment, 1.55f, top + 1.25f);
        }
        else
        {
            CreatePlatform(segment, new Vector2(-3.55f, 0f), 1.45f, top + 0.95f, 1.1f);
            CreatePlatform(segment, new Vector2(-1.35f, 0f), 0.95f, top + 1.45f, 1.0f);
            CreatePlatform(segment, new Vector2(1.3f, 0f), 1.15f, top + 0.85f, 1.1f);
            CreatePlatform(segment, new Vector2(3.45f, 0f), 1.45f, top, 1.2f);
            MaybeCreateFruitPickup(segment, -1.35f, top + 1.45f);
        }
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
        float gapWidth = Random.Range(2.75f, 3.55f);
        float gapCenter = Random.Range(-0.45f, 0.45f);
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
                speedIncreasePerSecond = 0.004f;
                maximumSpeed = 8.5f;
                obstacleChance = 0.18f;
                bonusPlatformChance = 0.06f;
                gapChance = 0.18f;
                challengeSegmentChance = 0.24f;
                decorationChance = 0.46f;
                break;
            case RunnerMode.Hard:
                scrollSpeed = Mathf.Max(scrollSpeed, 7.3f);
                speedIncreasePerSecond = 0.014f;
                maximumSpeed = 13f;
                obstacleChance = 0.42f;
                bonusPlatformChance = 0.12f;
                gapChance = 0.36f;
                challengeSegmentChance = 0.54f;
                decorationChance = 0.36f;
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

        CreateHudBackdrop(canvasGO.transform, "ScoreBackdrop", new Vector2(1f, 1f), new Vector2(-30f, -24f),
            new Vector2(430f, 118f), new Vector2(1f, 1f));

        var textGO = new GameObject("TimerScoreText");
        textGO.transform.SetParent(canvasGO.transform, false);
        _hudText = textGO.AddComponent<Text>();
        _hudText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _hudText.fontSize = 34;
        _hudText.alignment = TextAnchor.UpperRight;
        _hudText.color = new Color(1f, 1f, 0.92f, 1f);
        _hudText.horizontalOverflow = HorizontalWrapMode.Overflow;
        _hudText.verticalOverflow = VerticalWrapMode.Overflow;
        AddTextOutline(_hudText, new Color(0f, 0f, 0f, 0.95f), new Vector2(2.5f, -2.5f));

        var rect = _hudText.rectTransform;
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-36f, -28f);
        rect.sizeDelta = new Vector2(420f, 120f);

        CreateHealthHud(canvasGO.transform);
        UpdateHud();
    }

    void CreateHealthHud(Transform parent)
    {
        CreateHudBackdrop(parent, "HealthBackdrop", new Vector2(0f, 1f), new Vector2(28f, -24f),
            new Vector2(300f, 76f), new Vector2(0f, 1f));

        var labelGO = new GameObject("HealthText");
        labelGO.transform.SetParent(parent, false);
        _healthText = labelGO.AddComponent<Text>();
        _healthText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _healthText.fontSize = 28;
        _healthText.alignment = TextAnchor.UpperLeft;
        _healthText.color = new Color(1f, 1f, 0.92f, 1f);
        _healthText.horizontalOverflow = HorizontalWrapMode.Overflow;
        AddTextOutline(_healthText, new Color(0f, 0f, 0f, 0.95f), new Vector2(2f, -2f));

        var labelRect = _healthText.rectTransform;
        labelRect.anchorMin = new Vector2(0f, 1f);
        labelRect.anchorMax = new Vector2(0f, 1f);
        labelRect.pivot = new Vector2(0f, 1f);
        labelRect.anchoredPosition = new Vector2(36f, -28f);
        labelRect.sizeDelta = new Vector2(260f, 44f);

        var barBack = new GameObject("HealthBarBack");
        barBack.transform.SetParent(parent, false);
        var backImage = barBack.AddComponent<Image>();
        backImage.color = new Color(0.04f, 0.06f, 0.07f, 0.78f);
        var backRect = backImage.rectTransform;
        backRect.anchorMin = new Vector2(0f, 1f);
        backRect.anchorMax = new Vector2(0f, 1f);
        backRect.pivot = new Vector2(0f, 1f);
        backRect.anchoredPosition = new Vector2(36f, -68f);
        backRect.sizeDelta = new Vector2(260f, 20f);

        var barFill = new GameObject("HealthBarFill");
        barFill.transform.SetParent(barBack.transform, false);
        _healthFillImage = barFill.AddComponent<Image>();
        _healthFillImage.color = new Color(0.2f, 0.88f, 0.35f, 0.95f);
        _healthFillImage.type = Image.Type.Filled;
        _healthFillImage.fillMethod = Image.FillMethod.Horizontal;
        _healthFillImage.fillOrigin = 0;
        var fillRect = _healthFillImage.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = new Vector2(3f, 3f);
        fillRect.offsetMax = new Vector2(-3f, -3f);

        var damageGO = new GameObject("DamageText");
        damageGO.transform.SetParent(parent, false);
        _damageText = damageGO.AddComponent<Text>();
        _damageText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _damageText.fontSize = 30;
        _damageText.alignment = TextAnchor.UpperLeft;
        _damageText.color = new Color(1f, 0.22f, 0.18f, 0f);
        _damageText.horizontalOverflow = HorizontalWrapMode.Overflow;
        AddTextOutline(_damageText, new Color(0f, 0f, 0f, 0.95f), new Vector2(2f, -2f));
        var damageRect = _damageText.rectTransform;
        damageRect.anchorMin = new Vector2(0f, 1f);
        damageRect.anchorMax = new Vector2(0f, 1f);
        damageRect.pivot = new Vector2(0f, 1f);
        damageRect.anchoredPosition = new Vector2(310f, -54f);
        damageRect.sizeDelta = new Vector2(220f, 48f);

        var pickupGO = new GameObject("PickupText");
        pickupGO.transform.SetParent(parent, false);
        _pickupText = pickupGO.AddComponent<Text>();
        _pickupText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _pickupText.fontSize = 28;
        _pickupText.alignment = TextAnchor.UpperLeft;
        _pickupText.color = new Color(1f, 0.92f, 0.25f, 0f);
        _pickupText.horizontalOverflow = HorizontalWrapMode.Overflow;
        AddTextOutline(_pickupText, new Color(0f, 0f, 0f, 0.95f), new Vector2(2f, -2f));
        var pickupRect = _pickupText.rectTransform;
        pickupRect.anchorMin = new Vector2(0f, 1f);
        pickupRect.anchorMax = new Vector2(0f, 1f);
        pickupRect.pivot = new Vector2(0f, 1f);
        pickupRect.anchoredPosition = new Vector2(36f, -102f);
        pickupRect.sizeDelta = new Vector2(430f, 46f);
    }

    void CreateHudBackdrop(Transform parent, string name, Vector2 anchor, Vector2 anchoredPosition,
        Vector2 size, Vector2 pivot)
    {
        var panel = new GameObject(name);
        panel.transform.SetParent(parent, false);
        var image = panel.AddComponent<Image>();
        image.color = new Color(0.015f, 0.035f, 0.045f, 0.62f);
        image.raycastTarget = false;

        var rect = image.rectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    void AddTextOutline(Text text, Color color, Vector2 distance)
    {
        var outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = color;
        outline.effectDistance = distance;
        outline.useGraphicAlpha = true;
    }

    void CreateDeathFeedback(Vector3 worldPosition, string reason)
    {
        var root = new GameObject("DeathFeedback");
        root.transform.position = new Vector3(worldPosition.x, worldPosition.y + 0.7f, -0.5f);
        Destroy(root, deathFeedbackSeconds + 0.2f);

        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI * 2f / 8f;
            var shard = CreateSpriteVisual(root.transform, _solidSprite,
                new Vector3(Mathf.Cos(angle) * 0.28f, Mathf.Sin(angle) * 0.2f, 0f),
                0.18f, 0.18f, 30);
            if (shard == null)
                continue;

            shard.name = "DeathFlash";
            var renderer = shard.GetComponent<SpriteRenderer>();
            if (renderer != null)
                renderer.color = deathEffectColor;
            StartCoroutine(FadeAndDrift(shard.transform, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * 0.75f));
        }

        var reasonGO = new GameObject("DeathReasonText");
        reasonGO.transform.SetParent(root.transform, false);
        reasonGO.transform.localPosition = new Vector3(0.25f, 0.55f, 0f);

        var text = reasonGO.AddComponent<TextMesh>();
        text.text = reason;
        text.fontSize = 34;
        text.characterSize = 0.08f;
        text.anchor = TextAnchor.MiddleLeft;
        text.alignment = TextAlignment.Left;
        text.color = deathReasonColor;

        var rendererText = reasonGO.GetComponent<MeshRenderer>();
        if (rendererText != null)
        {
            rendererText.sortingLayerName = "Default";
            rendererText.sortingOrder = 31;
        }
    }

    IEnumerator FadeAndDrift(Transform target, Vector3 localDrift)
    {
        if (target == null)
            yield break;

        var renderer = target.GetComponent<SpriteRenderer>();
        Vector3 start = target.localPosition;
        Vector3 end = start + localDrift;
        Vector3 startScale = target.localScale;
        Vector3 endScale = startScale * 0.25f;
        float elapsed = 0f;
        while (elapsed < deathFeedbackSeconds && target != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / deathFeedbackSeconds);
            target.localPosition = Vector3.Lerp(start, end, t);
            target.localScale = Vector3.Lerp(startScale, endScale, t);
            if (renderer != null)
            {
                Color color = deathEffectColor;
                color.a = Mathf.Lerp(deathEffectColor.a, 0f, t);
                renderer.color = color;
            }
            yield return null;
        }
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

        UpdateHealthHud();
    }

    void UpdateHealthHud()
    {
        if (_playerHealth == null || _healthFillImage == null || _healthText == null)
            return;

        float maxHealth = Mathf.Max(1f, _playerHealth.maxHealth);
        float currentHealth = Mathf.Clamp(_playerHealth.CurrentHealth, 0f, maxHealth);
        float ratio = currentHealth / maxHealth;
        _healthFillImage.fillAmount = ratio;
        _healthFillImage.color = Color.Lerp(new Color(0.95f, 0.18f, 0.12f, 0.95f),
            new Color(0.2f, 0.88f, 0.35f, 0.95f), ratio);
        _healthText.text = string.Format("HP {0:000}/{1:000}", Mathf.CeilToInt(currentHealth), Mathf.CeilToInt(maxHealth));
    }

    void OnPlayerDamaged(float amount, float currentHealth, float maxHealth)
    {
        UpdateHealthHud();
        if (_damageText == null)
            return;

        if (_damageTextRoutine != null)
            StopCoroutine(_damageTextRoutine);
        _damageTextRoutine = StartCoroutine(ShowDamageText(amount));
    }

    void OnPlayerHealed(float amount, float currentHealth, float maxHealth)
    {
        UpdateHealthHud();
    }

    void ShowPickupMessage(string message)
    {
        if (_pickupText == null)
            return;

        if (_pickupTextRoutine != null)
            StopCoroutine(_pickupTextRoutine);
        _pickupTextRoutine = StartCoroutine(ShowPickupText(message));
    }

    IEnumerator ShowPickupText(string message)
    {
        _pickupText.text = message;
        float elapsed = 0f;
        while (elapsed < 1.2f)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / 1.2f);
            float alpha = t < 0.75f ? 1f : Mathf.Lerp(1f, 0f, Mathf.InverseLerp(0.75f, 1f, t));
            _pickupText.color = new Color(1f, 0.92f, 0.25f, alpha);
            yield return null;
        }

        _pickupText.color = new Color(1f, 0.92f, 0.25f, 0f);
        _pickupTextRoutine = null;
    }

    IEnumerator ShowDamageText(float amount)
    {
        _damageText.text = string.Format("-{0:0} HP", amount);
        float elapsed = 0f;
        while (elapsed < 0.75f)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / 0.75f);
            Color color = new Color(1f, 0.22f, 0.18f, Mathf.Lerp(1f, 0f, t));
            _damageText.color = color;
            _damageText.rectTransform.anchoredPosition = Vector2.Lerp(new Vector2(310f, -54f), new Vector2(310f, -82f), t);
            yield return null;
        }

        _damageText.color = new Color(1f, 0.22f, 0.18f, 0f);
        _damageTextRoutine = null;
    }

    void CreatePlatform(Transform segment, Vector2 localOffset, float width, float top,
        float visualDepth = -1f, bool useEndCaps = true)
    {
        var platform = new GameObject("Platform");
        platform.layer = _platformLayer;
        platform.transform.SetParent(segment, false);
        platform.transform.localPosition = new Vector3(localOffset.x, top - 0.5f, 0f);

        var collider = platform.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(width, 1f);

        float depth = visualDepth > 0f ? visualDepth : Mathf.Lerp(1.25f, mainGroundDepth, Mathf.InverseLerp(2.4f, segmentWidth, width));
        int columns = Mathf.Max(1, Mathf.CeilToInt(width));
        float tileWidth = width / columns;
        float left = -width * 0.5f + tileWidth * 0.5f;

        for (int column = 0; column < columns; column++)
        {
            float x = left + column * tileWidth;
            var surface = PickSurfaceSpriteForColumn(column, columns, useEndCaps);
            if (surface != null)
                CreateSpriteVisual(platform.transform, surface, new Vector3(x, 0.52f, 0f),
                    tileWidth + surfaceTileOverlap, 1f + surfaceTileOverlap, 1);
        }
    }

    void CreateFloatingPlatform(Transform segment, float localX, float width, float top)
    {
        CreatePlatform(segment, new Vector2(localX, 0f), width, top, 1.05f);
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

    void MaybeCreateFruitPickup(Transform segment, float localX, float platformTop)
    {
        if (_fruitSprites.Count == 0 || Random.value > highPlatformPickupChance)
            return;

        CreateFruitPickup(segment, localX, platformTop + 1.05f, PickFruitType());
    }

    void CreateFruitPickup(Transform segment, float localX, float localY, RunnerFruitPickup2D.FruitType type)
    {
        var pickup = new GameObject("FruitPickup_" + type);
        pickup.transform.SetParent(segment, false);
        pickup.transform.localPosition = new Vector3(localX, localY, 0f);

        var collider = pickup.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.34f;

        var fruit = pickup.AddComponent<RunnerFruitPickup2D>();
        fruit.type = type;
        fruit.healAmount = healFruitAmount;
        fruit.abilityDuration = type == RunnerFruitPickup2D.FruitType.Roll ? rollDuration : doubleJumpDuration;

        var sprite = PickFruitSprite(type);
        if (sprite != null)
            CreateSpriteVisual(pickup.transform, sprite, Vector3.zero, 0.7f, 0.7f, 5);
    }

    RunnerFruitPickup2D.FruitType PickFruitType()
    {
        float rollWeight = Mathf.Max(0f, 1f - healFruitWeight - doubleJumpFruitWeight);
        float total = Mathf.Max(0.01f, healFruitWeight + doubleJumpFruitWeight + rollWeight);
        float value = Random.value * total;
        if (value < healFruitWeight)
            return RunnerFruitPickup2D.FruitType.Heal;
        if (value < healFruitWeight + doubleJumpFruitWeight)
            return RunnerFruitPickup2D.FruitType.DoubleJump;
        return RunnerFruitPickup2D.FruitType.Roll;
    }

    Sprite PickFruitSprite(RunnerFruitPickup2D.FruitType type)
    {
        if (_fruitSprites.Count == 0)
            return null;

        int index = 0;
        if (type == RunnerFruitPickup2D.FruitType.DoubleJump)
            index = 1;
        else if (type == RunnerFruitPickup2D.FruitType.Roll)
            index = 2;
        return _fruitSprites[Mathf.Min(index, _fruitSprites.Count - 1)];
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
    }

    Sprite PickSurfaceSpriteForColumn(int column, int columns, bool useEndCaps)
    {
        if (_surfaceSprites == null || _surfaceSprites.Count == 0)
            return null;
        if (_surfaceSprites.Count < 4)
            return PickSprite(_surfaceSprites, column);
        if (!useEndCaps || columns <= 1)
            return _surfaceSprites[1];
        if (column == 0)
            return _surfaceSprites[0];
        if (column == columns - 1)
            return _surfaceSprites[3];
        return _surfaceSprites[1 + (column % 2)];
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

    GameObject CreateSolidVisual(Transform parent, Vector3 localPosition, float targetWidth, float targetHeight, int sortingOrder)
    {
        var visual = CreateSpriteVisual(parent, _solidSprite, localPosition, targetWidth, targetHeight, sortingOrder);
        if (visual == null)
            return null;

        var renderer = visual.GetComponent<SpriteRenderer>();
        if (renderer != null)
            renderer.color = terrainFillColor;
        return visual;
    }
}

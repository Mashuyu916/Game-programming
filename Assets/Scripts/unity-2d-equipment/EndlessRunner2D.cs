using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
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
    const string BestScoreKey = "EndlessRunnerBestScore";
    const string CoinWalletKey = "EndlessRunnerCoins";
    const int ShieldCost = 15;
    const int DoubleJumpCost = 20;
    const int FlightCost = 35;
    const int ReviveCost = 45;
    static EndlessRunner2D _activeRunner;
    static bool _showIntroOnNextGameplayLoad;
    static readonly float[] StoryMilestoneTimes = { 30f, 60f, 90f, 120f };
    static readonly string[] StoryMilestoneMessages =
    {
        "THE THUNDER LORD HAS FOUND YOU\nTHE STORM IS GAINING",
        "THE CORE ANSWERS YOUR COURAGE\nITS FLAME BURNS BRIGHTER",
        "THE SKY GATE IS NEAR\nDO NOT LET THE LIGHTNING TAKE THE CORE",
        "YOU HAVE BROKEN THE THUNDER PRISON\nKEEP RUNNING BEYOND THE STORM"
    };

    public enum RunnerMode
    {
        Easy,
        Normal,
        Hard
    }

    [Header("Scroll")]
    public float scrollSpeed = 6.5f;
    public float speedIncreasePerSecond = 0.008f;
    public float speedIncreasePerInterval = 0.45f;
    public float speedIncreaseIntervalSeconds = 30f;
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
    public float flightDuration = 10f;

    [Header("Coins")]
    [Range(0f, 1f)] public float coinSpawnChance = 0.58f;
    public int maximumCoinsPerSegment = 4;

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
    Sprite _coinSprite;
    Transform _player;
    Rigidbody2D _playerBody;
    PlayerAnimatorBridge _playerAnimator;
    PlayerHealthReload _playerHealth;
    Text _hudText;
    Image _healthFillImage;
    Text _healthText;
    Text _damageText;
    Text _pickupText;
    Text _storyText;
    Text _introStoryText;
    Text _coinHudText;
    Text _abilityTimerText;
    GameObject _abilityTimerBackdrop;
    GameObject _gameOverPanel;
    GameObject _readyPanel;
    GameObject _introStoryPanel;
    GameObject _shopPanel;
    Text _readyCoinText;
    Text _shieldShopText;
    Text _doubleJumpShopText;
    Text _flightShopText;
    Text _reviveShopText;
    Button _shieldShopButton;
    Button _doubleJumpShopButton;
    Button _flightShopButton;
    Button _reviveShopButton;
    Text _finalScoreText;
    Text _bestScoreText;
    Text _survivalTimeText;
    Text _storyResultText;
    Text _newBestText;
    Coroutine _damageTextRoutine;
    Coroutine _pickupTextRoutine;
    Coroutine _storyTextRoutine;
    Coroutine _introStoryRoutine;
    Font _storyFont;
    int _platformLayer;
    float _nextX;
    float _elapsedSeconds;
    float _currentTerrainTop;
    float _startingScrollSpeed;
    int _speedMinuteLevel;
    bool _lastSegmentHadGap;
    bool _isRestarting;
    bool _gameOverVisible;
    bool _runStarted;
    bool _introPlaying;
    bool _introShown;
    bool _pendingShield;
    bool _pendingDoubleJump;
    bool _pendingFlight;
    bool _pendingRevive;
    bool _activeRevive;
    float _shieldUntil;
    float _readyInputUnlockTime;
    int _challengeCooldown;
    int _builtSegments;
    int _storyMilestoneIndex;
    RunnerLightningChase2D _lightningChase;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void RegisterSceneLoader()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateForInitialGameplayScene()
    {
        CreateForGameplayScene(SceneManager.GetActiveScene());
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CreateForGameplayScene(scene);
    }

    static void CreateForGameplayScene(Scene scene)
    {
        if (scene.name != GameplayScene)
            return;
        if (FindObjectOfType<EndlessRunner2D>() != null)
            return;

        new GameObject("EndlessRunner").AddComponent<EndlessRunner2D>();
    }

    void Awake()
    {
        _introShown = !_showIntroOnNextGameplayLoad;
        _showIntroOnNextGameplayLoad = false;
        _activeRunner = this;
        ApplyModeSettings();
        _startingScrollSpeed = scrollSpeed;
        _platformLayer = LayerMask.NameToLayer(PlayerMovement2D.PlatformLayerName);
        LoadCuratedPaletteSprites();
        _coinSprite = Resources.Load<Sprite>("RunnerCoin");
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

        EnterReadyState();
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

    public static void ShowIntroOnNextGameplayLoad()
    {
        _showIntroOnNextGameplayLoad = true;
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

    public static void AddCoins(int amount)
    {
        if (amount <= 0)
            return;

        int wallet = PlayerPrefs.GetInt(CoinWalletKey, 0) + amount;
        PlayerPrefs.SetInt(CoinWalletKey, wallet);
        PlayerPrefs.Save();

        if (_activeRunner != null)
        {
            _activeRunner.ShowPickupMessage("COIN +" + amount);
            _activeRunner.UpdateCoinDisplays();
        }
    }

    void Update()
    {
        if (_gameOverVisible &&
            (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)))
        {
            RestartRunner();
            return;
        }

        if (!_runStarted)
        {
            if (_introPlaying)
            {
                if (Input.GetKeyDown(KeyCode.Space) ||
                    Input.GetKeyDown(KeyCode.K) ||
                    Input.GetKeyDown(KeyCode.Return))
                {
                    FinishIntroAndStartRun();
                }
                UpdateHud();
                return;
            }

            if (_shopPanel != null && _shopPanel.activeSelf)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                    CloseShop();
                UpdateHud();
                return;
            }

            if (!_gameOverVisible &&
                Time.unscaledTime >= _readyInputUnlockTime &&
                (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.K)))
            {
                if (_introShown)
                    BeginPreparedRun();
                else
                    StartIntroSequence();
            }
            UpdateHud();
            return;
        }

        if (!_isRestarting)
        {
            _elapsedSeconds += Time.deltaTime;
            UpdateStoryProgress();
        }
        UpdateHud();
    }

    void FixedUpdate()
    {
        if (_isRestarting || !_runStarted)
            return;

        scrollSpeed = Mathf.Min(maximumSpeed, scrollSpeed + speedIncreasePerSecond * Time.fixedDeltaTime);
        int speedLevel = Mathf.FloorToInt(_elapsedSeconds / Mathf.Max(1f, speedIncreaseIntervalSeconds));
        if (speedLevel > _speedMinuteLevel)
        {
            int gainedLevels = speedLevel - _speedMinuteLevel;
            _speedMinuteLevel = speedLevel;
            scrollSpeed = Mathf.Min(maximumSpeed, scrollSpeed + speedIncreasePerInterval * gainedLevels);
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
        _gameOverVisible = false;
        if (_gameOverPanel != null)
            _gameOverPanel.SetActive(false);
        _isRestarting = false;
        scrollSpeed = _startingScrollSpeed;
        _elapsedSeconds = 0f;
        _builtSegments = 0;
        _lastSegmentHadGap = false;
        _challengeCooldown = 0;
        _storyMilestoneIndex = 0;
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
            _playerBody.simulated = true;
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
        EnterReadyState();
        UpdateHud();
    }

    void ResetPickupAbilities()
    {
        if (_player == null)
            return;

        var movement = _player.GetComponent<PlayerMovement2D>();
        if (movement != null)
            movement.ClearDoubleJumpAbility();

        var flight = _player.GetComponent<PlayerFlight2D>();
        if (flight != null)
            flight.ClearFlightAbility();
    }

    void EnterReadyState()
    {
        _runStarted = false;
        _introPlaying = false;
        if (_introStoryRoutine != null)
        {
            StopCoroutine(_introStoryRoutine);
            _introStoryRoutine = null;
        }
        ClearStoryMessage();
        _readyInputUnlockTime = Time.unscaledTime + 0.45f;
        if (_readyPanel != null)
            _readyPanel.SetActive(true);
        if (_introStoryPanel != null)
            _introStoryPanel.SetActive(false);
        if (_shopPanel != null)
            _shopPanel.SetActive(false);

        if (_playerBody != null)
        {
            PlacePlayerAtGroundTop(groundTop);
            _playerBody.velocity = Vector2.zero;
            _playerBody.simulated = false;
        }

        if (_playerAnimator != null)
            _playerAnimator.ForceIdle();
        if (_lightningChase != null)
            _lightningChase.SetChasing(false);

        UpdateReadyShopUI();
    }

    void StartIntroSequence()
    {
        if (_introPlaying)
            return;

        _introShown = true;
        _introPlaying = true;
        if (_readyPanel != null)
            _readyPanel.SetActive(false);
        if (_shopPanel != null)
            _shopPanel.SetActive(false);
        if (_introStoryPanel != null)
            _introStoryPanel.SetActive(true);

        if (_introStoryRoutine != null)
            StopCoroutine(_introStoryRoutine);
        _introStoryRoutine = StartCoroutine(PlayIntroStory());
    }

    IEnumerator PlayIntroStory()
    {
        string[] lines =
        {
            "THE THUNDER LORD STOLE THE SKYFIRE CORE.",
            "ONE SAMURAI TOOK IT BACK.",
            "NOW THE STORM HUNTS ITS THIEF.",
            "REACH THE SKY GATE."
        };

        foreach (string line in lines)
        {
            yield return FadeIntroLine(line, 1.35f);
            if (!_introPlaying)
                yield break;
        }

        _introStoryRoutine = null;
        _introPlaying = false;
        if (_introStoryPanel != null)
            _introStoryPanel.SetActive(false);
        BeginPreparedRun();
    }

    IEnumerator FadeIntroLine(string line, float duration)
    {
        if (_introStoryText == null)
            yield break;

        _introStoryText.text = line;
        float elapsed = 0f;
        const float fadeTime = 0.28f;
        while (elapsed < duration && _introPlaying)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = 1f;
            if (elapsed < fadeTime)
                alpha = Mathf.Clamp01(elapsed / fadeTime);
            else if (elapsed > duration - fadeTime)
                alpha = Mathf.Clamp01((duration - elapsed) / fadeTime);

            _introStoryText.color = new Color(1f, 0.82f, 0.36f, alpha);
            yield return null;
        }
    }

    void FinishIntroAndStartRun()
    {
        if (!_introPlaying)
            return;

        _introPlaying = false;
        if (_introStoryRoutine != null)
        {
            StopCoroutine(_introStoryRoutine);
            _introStoryRoutine = null;
        }
        if (_introStoryPanel != null)
            _introStoryPanel.SetActive(false);

        BeginPreparedRun();
    }

    void BeginPreparedRun()
    {
        _runStarted = true;

        if (_playerBody != null)
        {
            _playerBody.simulated = true;
            _playerBody.velocity = Vector2.zero;
        }

        var movement = _player != null ? _player.GetComponent<PlayerMovement2D>() : null;
        if (movement != null)
        {
            movement.SuppressNextJumpInput();
            if (_pendingDoubleJump)
                movement.EnableDoubleJumpAbility(doubleJumpDuration);
        }

        if (_pendingFlight && _player != null)
        {
            var flight = _player.GetComponent<PlayerFlight2D>();
            if (flight != null)
                flight.EnableFlight(flightDuration);
        }

        if (_pendingShield && _player != null)
        {
            var invincibility = _player.GetComponent<PlayerInvincibility>();
            if (invincibility != null)
                invincibility.AddSeconds(8f);
            _shieldUntil = Time.time + 8f;
        }

        _activeRevive = _pendingRevive;
        _pendingShield = false;
        _pendingDoubleJump = false;
        _pendingFlight = false;
        _pendingRevive = false;

        if (_playerAnimator != null)
            _playerAnimator.endlessRunnerVisualSpeed = scrollSpeed;
        if (_lightningChase != null)
        {
            _lightningChase.ResetChase();
            _lightningChase.SetChasing(true);
        }
        ShowPickupMessage("GO!");
    }

    void BeginDeathRestart(string reason, Vector3 worldPosition, GameObject source)
    {
        if (_isRestarting)
            return;

        if (_activeRevive)
        {
            _activeRevive = false;
            if (_playerHealth != null)
                _playerHealth.RestoreFullHealth();
            if (_player != null)
            {
                PlacePlayerAtGroundTop(groundTop);
                var invincibility = _player.GetComponent<PlayerInvincibility>();
                if (invincibility != null)
                    invincibility.AddSeconds(2.5f);
                CreateReviveFeedback(_player.position);
            }
            if (_playerBody != null)
            {
                _playerBody.simulated = true;
                _playerBody.velocity = Vector2.zero;
            }
            ShowPickupMessage("REVIVED");
            return;
        }

        _isRestarting = true;
        if (_playerBody != null)
        {
            _playerBody.velocity = Vector2.zero;
            _playerBody.simulated = false;
        }

        StartCoroutine(DeathRestartRoutine(FormatDeathReason(reason, source), worldPosition));
    }

    IEnumerator DeathRestartRoutine(string reason, Vector3 worldPosition)
    {
        CreateDeathFeedback(worldPosition, reason);
        yield return new WaitForSeconds(deathFeedbackSeconds);
        ShowGameOver();
    }

    void ShowGameOver()
    {
        int finalScore = Mathf.FloorToInt(_elapsedSeconds * scorePerSecond);
        int bestScore = PlayerPrefs.GetInt(BestScoreKey, 0);
        bool newBest = finalScore > bestScore;
        if (newBest)
        {
            bestScore = finalScore;
            PlayerPrefs.SetInt(BestScoreKey, bestScore);
            PlayerPrefs.Save();
        }

        int totalSeconds = Mathf.FloorToInt(_elapsedSeconds);
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        if (_finalScoreText != null)
            _finalScoreText.text = string.Format("SCORE\n{0:000000}", finalScore);
        if (_bestScoreText != null)
            _bestScoreText.text = string.Format("BEST SCORE\n{0:000000}", bestScore);
        if (_survivalTimeText != null)
            _survivalTimeText.text = string.Format("SURVIVAL TIME\n{0:00}:{1:00}", minutes, seconds);
        if (_storyResultText != null)
            _storyResultText.text = GetStoryResult(totalSeconds);
        if (_newBestText != null)
            _newBestText.gameObject.SetActive(newBest);

        _gameOverVisible = true;
        if (_gameOverPanel != null)
            _gameOverPanel.SetActive(true);
    }

    string FormatDeathReason(string reason, GameObject source)
    {
        if (!string.IsNullOrEmpty(reason))
            return reason;
        if (source != null && source.GetComponent<EndlessRunnerObstacle2D>() != null)
            return "HIT AN OBSTACLE";
        return "DEATH";
    }

    void UpdateStoryProgress()
    {
        if (_storyMilestoneIndex >= StoryMilestoneTimes.Length ||
            _elapsedSeconds < StoryMilestoneTimes[_storyMilestoneIndex])
            return;

        ShowStoryMessage(StoryMilestoneMessages[_storyMilestoneIndex], 3.2f);
        _storyMilestoneIndex++;
    }

    string GetStoryResult(int totalSeconds)
    {
        if (totalSeconds >= 120)
            return "CHAPTER COMPLETE\nTHE SKYFIRE CORE IS FREE";
        if (totalSeconds >= 90)
            return "THE SKY GATE WAS WITHIN REACH";
        if (totalSeconds >= 60)
            return "THE CORE AWAKENED IN YOUR HANDS";
        if (totalSeconds >= 30)
            return "THE THUNDER LORD RECLAIMED THE TRAIL";
        return "THE ESCAPE HAS ONLY JUST BEGUN";
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

        if (_lightningChase == null)
        {
            var lightning = new GameObject("LightningChase");
            lightning.transform.SetParent(transform, false);
            _lightningChase = lightning.AddComponent<RunnerLightningChase2D>();
            _lightningChase.Initialize(_player);
        }

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
            dodge.enabled = false;

        var flight = player.GetComponent<PlayerFlight2D>();
        if (flight == null)
            flight = player.gameObject.AddComponent<PlayerFlight2D>();
        flight.enabled = true;

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

        MaybeCreateCoinTrail(segment, top, canMakeGap, canMakeChallenge);
        _lastSegmentHadGap = canMakeGap;
    }

    void MaybeCreateCoinTrail(Transform segment, float top, bool hasGap, bool isChallenge)
    {
        if (_coinSprite == null || Random.value > coinSpawnChance)
            return;

        int count = Random.Range(1, Mathf.Max(2, maximumCoinsPerSegment + 1));
        float spacing = hasGap ? 0.75f : 1.15f;
        float centerY = top + (hasGap ? 2.1f : isChallenge ? 2.7f : 1.25f);
        float startX = -(count - 1) * spacing * 0.5f;

        for (int i = 0; i < count; i++)
        {
            float x = startX + i * spacing;
            float arc = hasGap ? Mathf.Sin((i + 1f) / (count + 1f) * Mathf.PI) * 0.65f : 0f;
            CreateRunnerCoin(segment, new Vector3(x, centerY + arc, 0f));
        }
    }

    void CreateRunnerCoin(Transform segment, Vector3 localPosition)
    {
        var coin = new GameObject("RunnerCoin");
        coin.transform.SetParent(segment, false);
        coin.transform.localPosition = localPosition;

        var collider = coin.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.34f;
        coin.AddComponent<RunnerCoinPickup2D>();

        CreateSpriteVisual(coin.transform, _coinSprite, Vector3.zero, 0.72f, 0.72f, 6);
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

        _storyFont = CreateStoryFont();
        EnsureEventSystem();

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
        CreateCoinHud(canvasGO.transform);
        CreateAbilityTimerHud(canvasGO.transform);
        CreateStoryHud(canvasGO.transform);
        CreateIntroStoryPanel(canvasGO.transform);
        CreateGameOverPanel(canvasGO.transform);
        CreateReadyPanel(canvasGO.transform);
        UpdateHud();
    }

    void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
            return;

        var eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<StandaloneInputModule>();
    }

    void CreateGameOverPanel(Transform parent)
    {
        _gameOverPanel = new GameObject("GameOverPanel", typeof(RectTransform));
        _gameOverPanel.transform.SetParent(parent, false);
        var panelRect = _gameOverPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var dimmer = _gameOverPanel.AddComponent<Image>();
        dimmer.color = new Color(0.005f, 0.015f, 0.025f, 0.82f);

        var card = new GameObject("ResultsCard", typeof(RectTransform));
        card.transform.SetParent(_gameOverPanel.transform, false);
        var cardImage = card.AddComponent<Image>();
        cardImage.color = new Color(0.025f, 0.09f, 0.13f, 0.98f);
        var cardOutline = card.AddComponent<Outline>();
        cardOutline.effectColor = new Color(1f, 0.55f, 0.08f, 1f);
        cardOutline.effectDistance = new Vector2(5f, -5f);

        var cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(620f, 760f);

        var heading = CreateGameOverText(
            card.transform, "GameOverTitle", "GAME OVER", 48,
            new Vector2(0f, 300f), new Vector2(520f, 80f));
        heading.color = new Color(1f, 0.55f, 0.08f, 1f);
        heading.fontStyle = FontStyle.Bold;

        _finalScoreText = CreateGameOverText(
            card.transform, "FinalScore", "", 30,
            new Vector2(0f, 165f), new Vector2(500f, 110f));
        _finalScoreText.color = new Color(0.92f, 0.97f, 1f, 1f);

        var bestBackdrop = new GameObject("BestScoreHighlight", typeof(RectTransform));
        bestBackdrop.transform.SetParent(card.transform, false);
        var bestBackdropImage = bestBackdrop.AddComponent<Image>();
        bestBackdropImage.color = new Color(0.2f, 0.13f, 0.025f, 0.92f);
        var bestBackdropOutline = bestBackdrop.AddComponent<Outline>();
        bestBackdropOutline.effectColor = new Color(1f, 0.67f, 0.08f, 1f);
        bestBackdropOutline.effectDistance = new Vector2(3f, -3f);
        var bestBackdropRect = bestBackdrop.GetComponent<RectTransform>();
        bestBackdropRect.anchorMin = bestBackdropRect.anchorMax = new Vector2(0.5f, 0.5f);
        bestBackdropRect.anchoredPosition = new Vector2(0f, 15f);
        bestBackdropRect.sizeDelta = new Vector2(500f, 155f);

        _bestScoreText = CreateGameOverText(
            bestBackdrop.transform, "BestScore", "", 38,
            Vector2.zero, new Vector2(470f, 135f));
        _bestScoreText.color = new Color(1f, 0.76f, 0.12f, 1f);
        _bestScoreText.fontStyle = FontStyle.Bold;

        _newBestText = CreateGameOverText(
            card.transform, "NewBest", "NEW BEST!", 24,
            new Vector2(0f, -92f), new Vector2(400f, 45f));
        _newBestText.color = new Color(1f, 0.88f, 0.28f, 1f);
        _newBestText.fontStyle = FontStyle.Bold;
        _newBestText.gameObject.SetActive(false);

        _survivalTimeText = CreateGameOverText(
            card.transform, "SurvivalTime", "", 29,
            new Vector2(0f, -155f), new Vector2(500f, 100f));
        _survivalTimeText.color = new Color(0.82f, 0.94f, 1f, 1f);

        _storyResultText = CreateGameOverText(
            card.transform, "StoryResult", "", 22,
            new Vector2(0f, -225f), new Vector2(520f, 70f));
        _storyResultText.font = _storyFont;
        _storyResultText.color = new Color(1f, 0.72f, 0.18f, 1f);
        _storyResultText.fontStyle = FontStyle.Bold;

        var buttonGO = new GameObject("RunAgainButton", typeof(RectTransform));
        buttonGO.transform.SetParent(card.transform, false);
        var buttonRect = buttonGO.GetComponent<RectTransform>();
        buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(0f, -320f);
        buttonRect.sizeDelta = new Vector2(380f, 78f);

        var buttonImage = buttonGO.AddComponent<Image>();
        buttonImage.color = new Color(1f, 0.5f, 0.04f, 1f);
        var button = buttonGO.AddComponent<Button>();
        var colors = button.colors;
        colors.highlightedColor = new Color(1f, 0.72f, 0.14f, 1f);
        colors.pressedColor = new Color(0.86f, 0.3f, 0.02f, 1f);
        button.colors = colors;
        button.onClick.AddListener(RestartRunner);

        var buttonText = CreateGameOverText(
            buttonGO.transform, "Text", "RUN AGAIN", 32,
            Vector2.zero, new Vector2(360f, 70f));
        buttonText.fontStyle = FontStyle.Bold;
        buttonText.raycastTarget = false;

        _gameOverPanel.SetActive(false);
    }

    void CreateCoinHud(Transform parent)
    {
        CreateHudBackdrop(parent, "CoinBackdrop", new Vector2(0.5f, 1f), new Vector2(0f, -24f),
            new Vector2(230f, 58f), new Vector2(0.5f, 1f));

        _coinHudText = CreateGameOverText(
            parent, "CoinHudText", "", 27,
            new Vector2(0f, -52f), new Vector2(220f, 48f));
        _coinHudText.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        _coinHudText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        _coinHudText.rectTransform.pivot = new Vector2(0.5f, 1f);
        _coinHudText.color = new Color(1f, 0.8f, 0.12f, 1f);
        _coinHudText.fontStyle = FontStyle.Bold;
    }

    void CreateAbilityTimerHud(Transform parent)
    {
        _abilityTimerBackdrop = CreateHudBackdrop(parent, "AbilityTimerBackdrop", new Vector2(0f, 1f),
            new Vector2(28f, -150f), new Vector2(330f, 128f), new Vector2(0f, 1f));

        _abilityTimerText = CreateGameOverText(
            parent, "AbilityTimerText", "", 23,
            new Vector2(36f, -158f), new Vector2(310f, 112f));
        var rect = _abilityTimerText.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        _abilityTimerText.alignment = TextAnchor.UpperLeft;
        _abilityTimerText.color = new Color(0.8f, 0.95f, 1f, 1f);
    }

    void CreateStoryHud(Transform parent)
    {
        var storyGO = new GameObject("StoryText", typeof(RectTransform));
        storyGO.transform.SetParent(parent, false);
        _storyText = storyGO.AddComponent<Text>();
        _storyText.font = _storyFont;
        _storyText.fontSize = 32;
        _storyText.fontStyle = FontStyle.Bold;
        _storyText.alignment = TextAnchor.MiddleCenter;
        _storyText.color = new Color(1f, 0.78f, 0.2f, 0f);
        _storyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _storyText.verticalOverflow = VerticalWrapMode.Truncate;
        _storyText.raycastTarget = false;
        AddTextOutline(_storyText, new Color(0f, 0f, 0f, 0.95f), new Vector2(3f, -3f));

        var rect = _storyText.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.78f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(1000f, 110f);
    }

    void CreateIntroStoryPanel(Transform parent)
    {
        _introStoryPanel = new GameObject("IntroStoryPanel", typeof(RectTransform));
        _introStoryPanel.transform.SetParent(parent, false);
        var panelRect = _introStoryPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var dimmer = _introStoryPanel.AddComponent<Image>();
        dimmer.color = new Color(0.005f, 0.012f, 0.025f, 0.9f);
        dimmer.raycastTarget = true;

        var lineGO = new GameObject("IntroLine", typeof(RectTransform));
        lineGO.transform.SetParent(_introStoryPanel.transform, false);
        _introStoryText = lineGO.AddComponent<Text>();
        _introStoryText.font = _storyFont;
        _introStoryText.fontSize = 42;
        _introStoryText.fontStyle = FontStyle.Bold;
        _introStoryText.alignment = TextAnchor.MiddleCenter;
        _introStoryText.color = new Color(1f, 0.82f, 0.36f, 0f);
        _introStoryText.horizontalOverflow = HorizontalWrapMode.Wrap;
        _introStoryText.verticalOverflow = VerticalWrapMode.Truncate;
        _introStoryText.raycastTarget = false;
        AddTextOutline(_introStoryText, new Color(0f, 0f, 0f, 1f), new Vector2(3f, -3f));

        var lineRect = _introStoryText.rectTransform;
        lineRect.anchorMin = lineRect.anchorMax = new Vector2(0.5f, 0.53f);
        lineRect.pivot = new Vector2(0.5f, 0.5f);
        lineRect.sizeDelta = new Vector2(1250f, 150f);

        var skipText = CreateGameOverText(
            _introStoryPanel.transform, "SkipText", "SPACE / K / ENTER TO SKIP", 18,
            new Vector2(0f, -390f), new Vector2(500f, 45f));
        skipText.color = new Color(0.7f, 0.75f, 0.82f, 0.8f);
        skipText.raycastTarget = false;

        _introStoryPanel.SetActive(false);
    }

    Font CreateStoryFont()
    {
        Font font = Font.CreateDynamicFontFromOSFont(
            new[] { "Georgia", "Cambria", "Times New Roman" }, 32);
        return font != null
            ? font
            : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    void CreateReadyPanel(Transform parent)
    {
        _readyPanel = new GameObject("ReadyPanel", typeof(RectTransform));
        _readyPanel.transform.SetParent(parent, false);
        var fullRect = _readyPanel.GetComponent<RectTransform>();
        fullRect.anchorMin = Vector2.zero;
        fullRect.anchorMax = Vector2.one;
        fullRect.offsetMin = Vector2.zero;
        fullRect.offsetMax = Vector2.zero;

        var instructions = CreateGameOverText(
            _readyPanel.transform, "ReadyInstructions",
            "PRESS SPACE OR K TO START", 34,
            Vector2.zero, new Vector2(760f, 60f));
        var promptRect = instructions.rectTransform;
        promptRect.anchorMin = promptRect.anchorMax = new Vector2(0.5f, 0f);
        promptRect.pivot = new Vector2(0.5f, 0f);
        promptRect.anchoredPosition = new Vector2(0f, 42f);
        instructions.color = new Color(0.025f, 0.04f, 0.045f, 1f);
        instructions.fontStyle = FontStyle.Bold;
        var promptOutline = instructions.GetComponent<Outline>();
        if (promptOutline != null)
            promptOutline.enabled = false;

        var mission = CreateGameOverText(
            _readyPanel.transform, "StoryMission",
            "ESCAPE FROM THE THUNDER PRISON\n" +
            "Carry the stolen Skyfire Core to the Sky Gate.", 29,
            new Vector2(0f, -95f), new Vector2(920f, 100f));
        mission.font = _storyFont;
        var missionRect = mission.rectTransform;
        missionRect.anchorMin = missionRect.anchorMax = new Vector2(0.5f, 1f);
        missionRect.pivot = new Vector2(0.5f, 1f);
        mission.color = new Color(1f, 0.73f, 0.16f, 1f);
        mission.fontStyle = FontStyle.Bold;

        var shopButton = CreateReadyActionButton(
            _readyPanel.transform, "OpenShopButton", "SHOP", Vector2.zero, OpenShop);
        var shopRect = shopButton.GetComponent<RectTransform>();
        shopRect.anchorMin = shopRect.anchorMax = new Vector2(1f, 1f);
        shopRect.pivot = new Vector2(1f, 1f);
        shopRect.anchoredPosition = new Vector2(-72f, -118f);
        shopRect.sizeDelta = new Vector2(180f, 68f);
        shopButton.GetComponent<Image>().color = new Color(0.04f, 0.32f, 0.48f, 0.96f);

        CreateShopPanel(parent);
    }

    void CreateShopPanel(Transform parent)
    {
        _shopPanel = new GameObject("ShopPanel", typeof(RectTransform));
        _shopPanel.transform.SetParent(parent, false);
        var fullRect = _shopPanel.GetComponent<RectTransform>();
        fullRect.anchorMin = Vector2.zero;
        fullRect.anchorMax = Vector2.one;
        fullRect.offsetMin = Vector2.zero;
        fullRect.offsetMax = Vector2.zero;

        var dimmer = _shopPanel.AddComponent<Image>();
        dimmer.color = new Color(0.005f, 0.02f, 0.035f, 0.86f);

        var card = new GameObject("ShopCard", typeof(RectTransform));
        card.transform.SetParent(_shopPanel.transform, false);
        var cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(1500f, 920f);

        var cardImage = card.AddComponent<Image>();
        cardImage.sprite = Resources.Load<Sprite>("Shop/shop");
        cardImage.preserveAspect = true;
        cardImage.color = Color.white;
        var outline = card.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.68f, 0.08f, 1f);
        outline.effectDistance = new Vector2(5f, -5f);

        _readyCoinText = CreateGameOverText(
            card.transform, "ReadyCoins", "", 32,
            new Vector2(0f, 355f), new Vector2(700f, 55f));
        _readyCoinText.color = new Color(1f, 0.8f, 0.12f, 1f);
        _readyCoinText.fontStyle = FontStyle.Bold;

        var cardShelf = new GameObject("CardShelf", typeof(RectTransform));
        cardShelf.transform.SetParent(card.transform, false);
        var shelfImage = cardShelf.AddComponent<Image>();
        shelfImage.color = new Color(0.02f, 0.08f, 0.13f, 0.62f);
        var shelfRect = cardShelf.GetComponent<RectTransform>();
        shelfRect.anchorMin = shelfRect.anchorMax = new Vector2(0.5f, 0.5f);
        shelfRect.anchoredPosition = new Vector2(0f, -105f);
        shelfRect.sizeDelta = new Vector2(1260f, 380f);

        _shieldShopButton = CreateShopButton(card.transform, "ShieldShopButton", "Shop/shield-card",
            new Vector2(-450f, -105f), PurchaseShield, out _shieldShopText);
        _doubleJumpShopButton = CreateShopButton(card.transform, "DoubleJumpShopButton", "Shop/doublejump-card",
            new Vector2(-150f, -105f), PurchaseDoubleJump, out _doubleJumpShopText);
        _flightShopButton = CreateShopButton(card.transform, "FlightShopButton", "Shop/fly-card",
            new Vector2(150f, -105f), PurchaseFlight, out _flightShopText);
        _reviveShopButton = CreateShopButton(card.transform, "ReviveShopButton", "Shop/revive-card",
            new Vector2(450f, -105f), PurchaseRevive, out _reviveShopText);

        var hint = CreateGameOverText(
            card.transform, "ShopHint",
            "Boosts are consumed when the next run starts.", 21,
            new Vector2(0f, -365f), new Vector2(700f, 48f));
        hint.color = Color.white;

        CreateReadyActionButton(
            card.transform, "BackButton", "BACK", new Vector2(0f, -415f), CloseShop);

        UpdateReadyShopUI();
        _shopPanel.SetActive(false);
    }

    Button CreateReadyActionButton(Transform parent, string name, string text,
        Vector2 position, UnityEngine.Events.UnityAction action)
    {
        var buttonGO = new GameObject(name, typeof(RectTransform));
        buttonGO.transform.SetParent(parent, false);
        var rect = buttonGO.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(320f, 66f);

        var image = buttonGO.AddComponent<Image>();
        image.color = new Color(1f, 0.5f, 0.04f, 1f);
        var button = buttonGO.AddComponent<Button>();
        var colors = button.colors;
        colors.highlightedColor = Color.Lerp(image.color, Color.white, 0.24f);
        colors.pressedColor = Color.Lerp(image.color, Color.black, 0.2f);
        button.colors = colors;
        button.onClick.AddListener(action);

        var label = CreateGameOverText(
            buttonGO.transform, "Text", text, 28, Vector2.zero, new Vector2(300f, 58f));
        label.fontStyle = FontStyle.Bold;
        label.raycastTarget = false;
        return button;
    }

    void OpenShop()
    {
        if (_shopPanel == null)
            return;

        if (_readyPanel != null)
            _readyPanel.SetActive(false);
        _shopPanel.SetActive(true);
        SetShopButtonsInteractable(false);
        StartCoroutine(UnlockShopButtons());
    }

    void CloseShop()
    {
        if (_shopPanel != null)
            _shopPanel.SetActive(false);
        if (_readyPanel != null)
            _readyPanel.SetActive(true);
        _readyInputUnlockTime = Time.unscaledTime + 0.2f;
    }

    IEnumerator UnlockShopButtons()
    {
        yield return new WaitForSecondsRealtime(0.25f);
        UpdateReadyShopUI();
    }

    void SetShopButtonsInteractable(bool interactable)
    {
        if (_shieldShopButton != null)
            _shieldShopButton.interactable = interactable;
        if (_doubleJumpShopButton != null)
            _doubleJumpShopButton.interactable = interactable;
        if (_flightShopButton != null)
            _flightShopButton.interactable = interactable;
        if (_reviveShopButton != null)
            _reviveShopButton.interactable = interactable;
    }

    Button CreateShopButton(Transform parent, string name, string resourcePath, Vector2 position,
        UnityEngine.Events.UnityAction action, out Text label)
    {
        var buttonGO = new GameObject(name, typeof(RectTransform));
        buttonGO.transform.SetParent(parent, false);
        var rect = buttonGO.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(270f, 330f);

        var image = buttonGO.AddComponent<Image>();
        image.sprite = Resources.Load<Sprite>(resourcePath);
        image.preserveAspect = true;
        image.color = Color.white;

        var button = buttonGO.AddComponent<Button>();
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.94f, 0.72f, 1f);
        colors.pressedColor = new Color(0.88f, 0.78f, 0.52f, 1f);
        colors.disabledColor = Color.white;
        button.colors = colors;
        button.onClick.AddListener(action);

        label = CreateGameOverText(buttonGO.transform, "Price", "", 23,
            new Vector2(0f, -142f), new Vector2(250f, 48f));
        label.fontStyle = FontStyle.Bold;
        label.color = new Color(1f, 0.84f, 0.24f, 1f);
        label.raycastTarget = false;
        return button;
    }

    void PurchaseShield()
    {
        PurchaseBoost(ShieldCost, ref _pendingShield, "START SHIELD SELECTED");
    }

    void PurchaseDoubleJump()
    {
        PurchaseBoost(DoubleJumpCost, ref _pendingDoubleJump, "DOUBLE JUMP SELECTED");
    }

    void PurchaseFlight()
    {
        PurchaseBoost(FlightCost, ref _pendingFlight, "CLOUD FLIGHT SELECTED");
    }

    void PurchaseRevive()
    {
        PurchaseBoost(ReviveCost, ref _pendingRevive, "REVIVE SELECTED");
    }

    void PurchaseBoost(int cost, ref bool selected, string confirmation)
    {
        if (selected)
            return;

        int wallet = PlayerPrefs.GetInt(CoinWalletKey, 0);
        if (wallet < cost)
        {
            ShowPickupMessage("NOT ENOUGH COINS");
            return;
        }

        selected = true;
        PlayerPrefs.SetInt(CoinWalletKey, wallet - cost);
        PlayerPrefs.Save();
        ShowPickupMessage(confirmation);
        UpdateReadyShopUI();
    }

    void UpdateReadyShopUI()
    {
        int wallet = PlayerPrefs.GetInt(CoinWalletKey, 0);
        if (_readyCoinText != null)
            _readyCoinText.text = "COINS  " + wallet.ToString("000");

        UpdateShopButton(_shieldShopButton, _shieldShopText, _pendingShield,
            wallet >= ShieldCost, "START SHIELD  8s", ShieldCost);
        UpdateShopButton(_doubleJumpShopButton, _doubleJumpShopText, _pendingDoubleJump,
            wallet >= DoubleJumpCost, "START DOUBLE JUMP  14s", DoubleJumpCost);
        UpdateShopButton(_flightShopButton, _flightShopText, _pendingFlight,
            wallet >= FlightCost, "START CLOUD FLIGHT  10s", FlightCost);
        UpdateShopButton(_reviveShopButton, _reviveShopText, _pendingRevive,
            wallet >= ReviveCost, "ONE REVIVE", ReviveCost);
        UpdateCoinDisplays();
    }

    void UpdateShopButton(Button button, Text label, bool selected, bool affordable,
        string itemName, int cost)
    {
        if (button == null || label == null)
            return;

        button.interactable = true;
        label.text = selected ? "SELECTED" : cost + " COINS";
        label.color = selected
            ? new Color(0.45f, 1f, 0.55f, 1f)
            : affordable
                ? new Color(1f, 0.84f, 0.24f, 1f)
                : new Color(1f, 0.45f, 0.34f, 1f);
    }

    void UpdateCoinDisplays()
    {
        if (_coinHudText != null)
            _coinHudText.text = "COINS  " + PlayerPrefs.GetInt(CoinWalletKey, 0).ToString("000");
    }

    Text CreateGameOverText(Transform parent, string name, string content, int fontSize,
        Vector2 position, Vector2 size)
    {
        var textGO = new GameObject(name, typeof(RectTransform));
        textGO.transform.SetParent(parent, false);
        var text = textGO.AddComponent<Text>();
        text.text = content;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;

        var rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        AddTextOutline(text, new Color(0f, 0f, 0f, 0.95f), new Vector2(2f, -2f));
        return text;
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

    GameObject CreateHudBackdrop(Transform parent, string name, Vector2 anchor, Vector2 anchoredPosition,
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
        return panel;
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

    void CreateReviveFeedback(Vector3 worldPosition)
    {
        var effect = new GameObject("ReviveBurst");
        effect.transform.position = new Vector3(worldPosition.x, worldPosition.y + 0.45f, -0.5f);

        var particles = effect.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = particles.main;
        main.duration = 0.45f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 4.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.24f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.82f, 0.24f, 1f),
            new Color(0.35f, 0.92f, 1f, 1f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 36;

        var emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, 28)
        });

        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.38f;

        var colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.92f, 0.45f), 0f),
                new GradientColorKey(new Color(0.25f, 0.88f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0.85f, 0.45f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        var sizeOverLifetime = particles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
            1f, new AnimationCurve(
                new Keyframe(0f, 0.35f),
                new Keyframe(0.25f, 1f),
                new Keyframe(1f, 0f)));

        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = 40;

        particles.Play();
        Destroy(effect, 1.2f);
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
        UpdateCoinDisplays();
        UpdateAbilityTimerHud();
    }

    void UpdateAbilityTimerHud()
    {
        if (_abilityTimerText == null || _player == null)
            return;

        var lines = new List<string>();
        float shieldRemaining = Mathf.Max(0f, _shieldUntil - Time.time);
        if (shieldRemaining > 0f)
            lines.Add("SHIELD  " + Mathf.CeilToInt(shieldRemaining) + "s");

        var movement = _player.GetComponent<PlayerMovement2D>();
        if (movement != null && movement.DoubleJumpTimeRemaining > 0f)
            lines.Add("DOUBLE JUMP  " + Mathf.CeilToInt(movement.DoubleJumpTimeRemaining) + "s");

        var flight = _player.GetComponent<PlayerFlight2D>();
        if (flight != null && flight.FlightTimeRemaining > 0f)
            lines.Add("FLIGHT  " + Mathf.CeilToInt(flight.FlightTimeRemaining) + "s");

        if (_activeRevive)
            lines.Add("REVIVE  READY");

        _abilityTimerText.text = lines.Count == 0 ? "" : string.Join("\n", lines);
        if (_abilityTimerBackdrop != null)
            _abilityTimerBackdrop.SetActive(lines.Count > 0);
        _abilityTimerText.gameObject.SetActive(lines.Count > 0);
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

    void ShowStoryMessage(string message, float duration)
    {
        if (_storyText == null)
            return;

        if (_storyTextRoutine != null)
            StopCoroutine(_storyTextRoutine);
        _storyTextRoutine = StartCoroutine(ShowStoryText(message, duration));
    }

    void ClearStoryMessage()
    {
        if (_storyTextRoutine != null)
        {
            StopCoroutine(_storyTextRoutine);
            _storyTextRoutine = null;
        }

        if (_storyText != null)
            _storyText.color = new Color(1f, 0.78f, 0.2f, 0f);
    }

    IEnumerator ShowStoryText(string message, float duration)
    {
        _storyText.text = message;
        float elapsed = 0f;
        float fadeTime = Mathf.Min(0.4f, duration * 0.2f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f;
            if (elapsed < fadeTime)
                alpha = Mathf.Clamp01(elapsed / fadeTime);
            else if (elapsed > duration - fadeTime)
                alpha = Mathf.Clamp01((duration - elapsed) / fadeTime);

            _storyText.color = new Color(1f, 0.78f, 0.2f, alpha);
            yield return null;
        }

        _storyText.color = new Color(1f, 0.78f, 0.2f, 0f);
        _storyTextRoutine = null;
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
        fruit.abilityDuration = type == RunnerFruitPickup2D.FruitType.Flight ? flightDuration : doubleJumpDuration;

        var sprite = PickFruitSprite(type);
        if (sprite != null)
            CreateSpriteVisual(pickup.transform, sprite, Vector3.zero, 0.7f, 0.7f, 5);
    }

    RunnerFruitPickup2D.FruitType PickFruitType()
    {
        float flightWeight = Mathf.Max(0f, 1f - healFruitWeight - doubleJumpFruitWeight);
        float total = Mathf.Max(0.01f, healFruitWeight + doubleJumpFruitWeight + flightWeight);
        float value = Random.value * total;
        if (value < healFruitWeight)
            return RunnerFruitPickup2D.FruitType.Heal;
        if (value < healFruitWeight + doubleJumpFruitWeight)
            return RunnerFruitPickup2D.FruitType.DoubleJump;
        return RunnerFruitPickup2D.FruitType.Flight;
    }

    Sprite PickFruitSprite(RunnerFruitPickup2D.FruitType type)
    {
        if (_fruitSprites.Count == 0)
            return null;

        int index = 0;
        if (type == RunnerFruitPickup2D.FruitType.DoubleJump)
            index = 1;
        else if (type == RunnerFruitPickup2D.FruitType.Flight)
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

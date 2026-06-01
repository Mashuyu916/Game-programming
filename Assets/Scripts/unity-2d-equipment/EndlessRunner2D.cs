using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Replaces the old static gameplay map with recycled runner segments.
/// Existing tile sprites are harvested before the old Grid is removed.
/// </summary>
[DefaultExecutionOrder(-200)]
public class EndlessRunner2D : MonoBehaviour
{
    const string GameplayScene = "1";

    [Header("Scroll")]
    public float scrollSpeed = 6.5f;
    public float speedIncreasePerSecond = 0.035f;
    public float maximumSpeed = 11f;

    [Header("Track")]
    public float groundTop = -7.1f;
    public float segmentWidth = 8f;
    public int segmentCount = 9;
    public float recycleAtX = -16f;

    readonly List<Transform> _segments = new List<Transform>();
    readonly List<Sprite> _tileSprites = new List<Sprite>();
    Transform _player;
    Rigidbody2D _playerBody;
    PlayerAnimatorBridge _playerAnimator;
    int _platformLayer;
    float _nextX;
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
        _platformLayer = LayerMask.NameToLayer(PlayerMovement2D.PlatformLayerName);
        HarvestTileSprites();
        RemoveOldStaticLevel();
        ConfigurePlayerAndCamera();

        _nextX = -12f;
        for (int i = 0; i < segmentCount; i++)
            CreateSegment();
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
        var grid = GameObject.Find("Grid");
        if (grid != null)
        {
            grid.SetActive(false);
            Destroy(grid);
        }

        foreach (var enemy in GameObject.FindGameObjectsWithTag("Untagged"))
        {
            if (enemy.name == "Enemy")
            {
                enemy.SetActive(false);
                Destroy(enemy);
            }
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
        CreatePlatform(segment, Vector2.zero, segmentWidth, groundTop);

        if (index > 1 && Random.value < 0.62f)
        {
            float obstacleX = Random.Range(-segmentWidth * 0.2f, segmentWidth * 0.3f);
            CreateObstacle(segment, obstacleX);
        }

        if (index > 2 && Random.value < 0.38f)
        {
            float width = Random.Range(2f, 3.5f);
            float x = Random.Range(-segmentWidth * 0.25f, segmentWidth * 0.25f);
            CreatePlatform(segment, new Vector2(x, 0f), width, groundTop + Random.Range(2.1f, 2.8f));
        }
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
            CreateTileVisual(platform.transform, new Vector3(x, 0.52f, 0f), i);
        }
    }

    void CreateObstacle(Transform segment, float localX)
    {
        var obstacle = new GameObject("Obstacle");
        obstacle.transform.SetParent(segment, false);
        obstacle.transform.localPosition = new Vector3(localX, groundTop + 0.58f, 0f);
        obstacle.AddComponent<EndlessRunnerObstacle2D>();

        var collider = obstacle.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(0.8f, 1.15f);

        CreateTileVisual(obstacle.transform, Vector3.zero, _builtSegments + 3, 0.9f);
    }

    void CreateTileVisual(Transform parent, Vector3 localPosition, int spriteIndex, float targetSize = 1.05f)
    {
        if (_tileSprites.Count == 0)
            return;

        var visual = new GameObject("TileVisual");
        visual.transform.SetParent(parent, false);
        visual.transform.localPosition = localPosition;

        var renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = _tileSprites[Mathf.Abs(spriteIndex) % _tileSprites.Count];
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = 0;

        Vector2 spriteSize = renderer.sprite.bounds.size;
        float scale = targetSize / Mathf.Max(0.01f, Mathf.Max(spriteSize.x, spriteSize.y));
        visual.transform.localScale = new Vector3(scale, scale, 1f);
    }
}

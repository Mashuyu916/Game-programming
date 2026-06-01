using UnityEngine;

/// <summary>
/// Minimal platformer controller for a 2D player.
/// Attach to the Player root. Tilemap / platforms must use the "platform" layer.
/// Yields to <see cref="PlayerDodge2D"/> while a roll is active (runs before dodge via execution order).
/// </summary>
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerMovement2D : MonoBehaviour
{
    public const string PlatformLayerName = "platform";

    [Header("Movement")]
    public float moveSpeed = 7f;
    public float acceleration = 70f;
    public float deceleration = 70f;
    public bool allowAirControl = true;
    [Tooltip("Invert horizontal input if controls are reversed (fix drifting left/right)")]
    public bool invertHorizontal = false;
    [Tooltip("Endless runner mode: the level moves while the player only jumps.")]
    public bool endlessRunnerMode;

    [Header("Jump")]
    public float jumpForce = 12f;
    public float coyoteTime = 0.12f;
    public float jumpBufferTime = 0.12f;
    public float fallMultiplier = 2.5f;
    public float lowJumpMultiplier = 2f;

    [Header("Ground Detection")]
    [Tooltip("Layers counted as ground (platforms / tilemap). Should be the \"platform\" layer.")]
    public LayerMask groundLayer;
    [Tooltip("Small empty child at the feet, slightly below the collider bottom.")]
    public Transform groundCheck;
    [Tooltip("Legacy tuning value kept for existing scenes.")]
    public float groundCheckRadius = 0.2f;
    [Tooltip("Extra width used by the feet check so tile edges still count as ground.")]
    public float groundCheckWidthPadding = 0.08f;
    [Tooltip("Short distance checked below the feet.")]
    public float groundCheckDistance = 0.12f;

    [Header("Visual")]
    [Tooltip("Sprite that flips left/right. Usually PlayerVisual.")]
    public SpriteRenderer facingVisual;

    Rigidbody2D _rb;
    Collider2D _col;
    PlayerDodge2D _dodge;

    float _inputX;
    float _jumpBuffer;
    bool _jumpHeld;
    float _lastGroundedTime;
    bool _isGrounded;

    void Reset()
    {
        ApplyDefaultGroundLayer();
        EnsureGroundCheckChild();
    }

    void OnValidate()
    {
        ApplyDefaultGroundLayer();
        SnapGroundCheckToFeet();
    }

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _col = GetComponent<Collider2D>();
        _rb.freezeRotation = true;
        _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        _dodge = GetComponent<PlayerDodge2D>();
        ApplyDefaultGroundLayer();

        if (facingVisual == null)
            facingVisual = GetComponentInChildren<SpriteRenderer>();

        if (groundCheck == null)
            EnsureGroundCheckChild();

        SnapGroundCheckToFeet();
    }

    void Update()
    {
        _inputX = endlessRunnerMode
            ? 0f
            : Input.GetAxisRaw("Horizontal") * (invertHorizontal ? -1f : 1f);

        if (Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.K))
            _jumpBuffer = jumpBufferTime;

        _jumpHeld = Input.GetButton("Jump") || Input.GetKey(KeyCode.K);

        if (!Mathf.Approximately(_inputX, 0f))
            PlayerFacing2D.ApplyHorizontalFacing(transform, facingVisual, _inputX);
    }

    void FixedUpdate()
    {
        if (_dodge != null && _dodge.IsRollActive)
            return;

        bool grounded = IsGrounded();
        if (grounded)
            _lastGroundedTime = Time.time;

        if (_jumpBuffer > 0f)
            _jumpBuffer -= Time.fixedDeltaTime;

        Vector2 velocity = _rb.velocity;
        float targetX = _inputX * moveSpeed;

        if (!grounded && !allowAirControl)
            targetX = velocity.x;

        float accel = Mathf.Abs(targetX) > 0.01f ? acceleration : deceleration;
        velocity.x = Mathf.MoveTowards(velocity.x, targetX, accel * Time.fixedDeltaTime);

        if (_jumpBuffer > 0f && (grounded || Time.time - _lastGroundedTime <= coyoteTime))
        {
            velocity.y = jumpForce;
            _jumpBuffer = 0f;
        }

        if (velocity.y < 0f)
        {
            velocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1f) * Time.fixedDeltaTime;
        }
        else if (!_jumpHeld && velocity.y > 0f)
        {
            velocity += Vector2.up * Physics2D.gravity.y * (lowJumpMultiplier - 1f) * Time.fixedDeltaTime;
        }

        _rb.velocity = velocity;

    }

    bool IsGrounded()
    {
        if (groundCheck == null || _col == null)
            return false;

        Bounds bounds = _col.bounds;
        Vector2 feetCenter = new Vector2(bounds.center.x, bounds.min.y + 0.02f);
        Vector2 feetSize = new Vector2(
            Mathf.Max(0.1f, bounds.size.x - groundCheckWidthPadding * 2f),
            0.08f);
        return Physics2D.BoxCast(
            feetCenter, feetSize, 0f, Vector2.down, groundCheckDistance, groundLayer);
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
            return;
        Gizmos.color = IsGrounded() ? Color.green : Color.red;
        if (_col == null)
            _col = GetComponent<Collider2D>();
        if (_col == null)
            return;

        Bounds bounds = _col.bounds;
        Vector3 center = new Vector3(bounds.center.x, bounds.min.y - groundCheckDistance * 0.5f + 0.02f, 0f);
        Vector3 size = new Vector3(
            Mathf.Max(0.1f, bounds.size.x - groundCheckWidthPadding * 2f),
            groundCheckDistance + 0.08f, 0f);
        Gizmos.DrawWireCube(center, size);
    }

    void ApplyDefaultGroundLayer()
    {
        int platform = LayerMask.GetMask(PlatformLayerName);
        if (platform != 0)
            groundLayer = platform;
    }

    void EnsureGroundCheckChild()
    {
        var existing = transform.Find("GroundCheck");
        if (existing != null)
        {
            groundCheck = existing;
            return;
        }

        var go = new GameObject("GroundCheck");
        go.transform.SetParent(transform, false);
        groundCheck = go.transform;
        SnapGroundCheckToFeet();
    }

    void SnapGroundCheckToFeet()
    {
        if (groundCheck == null)
            return;

        var col = _col != null ? _col : GetComponent<Collider2D>();
        if (col == null)
        {
            groundCheck.localPosition = new Vector3(0f, -0.5f, 0f);
            return;
        }

        var b = col.bounds;
        Vector3 feet = new Vector3(b.center.x, b.min.y - groundCheckRadius * 0.5f, 0f);
        groundCheck.position = feet;
        groundCheck.localPosition = new Vector3(groundCheck.localPosition.x, groundCheck.localPosition.y, 0f);
    }
}

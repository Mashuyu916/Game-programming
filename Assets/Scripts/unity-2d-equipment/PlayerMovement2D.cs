using UnityEngine;

/// <summary>
/// Minimal platformer controller for an empty 2D project (Rigidbody2D + ground check).
/// Attach to the Player root. Tilemap / platforms must use the "platform" layer.
/// Yields to <see cref="PlayerDodge2D"/> while a roll is active (runs before dodge via execution order).
/// </summary>
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerMovement2D : MonoBehaviour
{
    public const string PlatformLayerName = "platform";

    public float moveSpeed = 7f;
    public float jumpForce = 12f;

    [Tooltip("Layers counted as ground (platforms / tilemap). Should be the \"platform\" layer.")]
    public LayerMask groundLayer;

    [Tooltip("Small empty child at the feet, slightly below the collider bottom.")]
    public Transform groundCheck;

    [Tooltip("Radius for overlap circle at ground check.")]
    public float groundCheckRadius = 0.2f;

    [Tooltip("Sprite that flips left/right. Usually PlayerVisual.")]
    public SpriteRenderer facingVisual;

    Rigidbody2D _rb;
    Collider2D _col;
    PlayerDodge2D _dodge;
    float _inputX;
    bool _jumpPressed;

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
        _inputX = Input.GetAxisRaw("Horizontal");
        if (Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.K))
            _jumpPressed = true;

        if (!Mathf.Approximately(_inputX, 0f))
            PlayerFacing2D.ApplyHorizontalFacing(transform, facingVisual, _inputX);
    }

    void FixedUpdate()
    {
        if (_dodge != null && _dodge.IsRollActive)
            return;

        bool grounded = IsGrounded();
        Vector2 v = _rb.velocity;
        v.x = _inputX * moveSpeed;

        if (_jumpPressed && grounded)
            v.y = jumpForce;

        _rb.velocity = v;
        _jumpPressed = false;

        if (!grounded && v.y < 0f)
            SnapOntoPlatformIfClose();
    }

    void SnapOntoPlatformIfClose()
    {
        if (_col == null || groundCheck == null)
            return;

        float dist = groundCheckRadius + 0.35f;
        var hit = Physics2D.Raycast(groundCheck.position, Vector2.down, dist, groundLayer);
        if (!hit.collider)
            return;

        float feetY = _col.bounds.min.y;
        float gap = feetY - hit.point.y;
        if (gap > 0f && gap < 0.5f)
        {
            var p = _rb.position;
            p.y -= gap;
            _rb.position = p;
            _rb.velocity = new Vector2(_rb.velocity.x, 0f);
        }
    }

    bool IsGrounded()
    {
        if (groundCheck == null)
            return false;

        if (Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer))
            return true;

        float rayLen = groundCheckRadius + 0.08f;
        return Physics2D.Raycast(groundCheck.position, Vector2.down, rayLen, groundLayer);
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
            return;
        Gizmos.color = IsGrounded() ? Color.green : Color.red;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
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
using UnityEngine;

/// <summary>
/// Minimal platformer controller for an empty 2D project (Rigidbody2D + ground check).
/// Attach to the Player root. Assign Ground layer on <see cref="groundLayer"/>.
/// Yields to <see cref="PlayerDodge2D"/> while a roll is active (runs before dodge via execution order).
/// </summary>
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement2D : MonoBehaviour
{
    public float moveSpeed = 7f;
    public float jumpForce = 12f;

    [Tooltip("Layers counted as ground (platforms / tilemap).")]
    public LayerMask groundLayer;

    [Tooltip("Small empty child at the feet, slightly below the collider bottom.")]
    public Transform groundCheck;

    [Tooltip("Radius for overlap circle at ground check.")]
    public float groundCheckRadius = 0.08f;

    Rigidbody2D _rb;
    PlayerDodge2D _dodge;
    float _inputX;
    bool _jumpPressed;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _rb.freezeRotation = true;
        _dodge = GetComponent<PlayerDodge2D>();
    }

    void Update()
    {
        _inputX = Input.GetAxisRaw("Horizontal");
        if (Input.GetButtonDown("Jump"))
            _jumpPressed = true;

        if (!Mathf.Approximately(_inputX, 0f))
        {
            var s = transform.localScale;
            s.x = Mathf.Abs(s.x) * Mathf.Sign(_inputX);
            transform.localScale = s;
        }
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
    }

    bool IsGrounded()
    {
        if (groundCheck == null)
            return false;
        return Physics2D.OverlapCircle(groundCheck.position, groundCheckRadius, groundLayer);
    }

    void OnDrawGizmosSelected()
    {
        if (groundCheck == null)
            return;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(groundCheck.position, groundCheckRadius);
    }
}
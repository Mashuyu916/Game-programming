using UnityEngine;

/// <summary>
/// Minimal platformer controller for a 2D player.
/// Attach to the Player root. Tilemap / platforms must use the "platform" layer.
/// Yields to <see cref="PlayerFlight2D"/> while cloud flight is active.
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

    [Header("Pickup Abilities")]
    public bool requirePickupForDoubleJump = true;
    public int extraJumpsWithPickup = 1;

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
    PlayerFlight2D _flight;

    float _inputX;
    float _jumpBuffer;
    bool _jumpHeld;
    float _lastGroundedTime;
    bool _isGrounded;
    float _doubleJumpUntil;
    int _extraJumpsRemaining;
    bool _suppressJumpInput;

    public bool HasDoubleJumpAbility => !requirePickupForDoubleJump || Time.time < _doubleJumpUntil;
    public float DoubleJumpTimeRemaining => Mathf.Max(0f, _doubleJumpUntil - Time.time);

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
        _flight = GetComponent<PlayerFlight2D>();
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

        if (_suppressJumpInput)
        {
            _suppressJumpInput = false;
        }
        else if (Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.K))
            _jumpBuffer = jumpBufferTime;

        _jumpHeld = Input.GetButton("Jump") || Input.GetKey(KeyCode.K);

        if (!Mathf.Approximately(_inputX, 0f))
            PlayerFacing2D.ApplyHorizontalFacing(transform, facingVisual, _inputX);
    }

    void FixedUpdate()
    {
        if (_flight == null)
            _flight = GetComponent<PlayerFlight2D>();
        if (_flight != null && _flight.IsFlying)
            return;

        bool grounded = IsGrounded();
        _isGrounded = grounded;
        if (grounded)
        {
            _lastGroundedTime = Time.time;
            _extraJumpsRemaining = HasDoubleJumpAbility ? extraJumpsWithPickup : 0;
        }

        if (_jumpBuffer > 0f)
            _jumpBuffer -= Time.fixedDeltaTime;

        Vector2 velocity = _rb.velocity;
        float targetX = _inputX * moveSpeed;

        if (!grounded && !allowAirControl)
            targetX = velocity.x;

        float accel = Mathf.Abs(targetX) > 0.01f ? acceleration : deceleration;
        velocity.x = Mathf.MoveTowards(velocity.x, targetX, accel * Time.fixedDeltaTime);

        bool canGroundJump = grounded || Time.time - _lastGroundedTime <= coyoteTime;
        bool canExtraJump = !canGroundJump && HasDoubleJumpAbility && _extraJumpsRemaining > 0;
        if (_jumpBuffer > 0f && (canGroundJump || canExtraJump))
        {
            velocity.y = jumpForce;
            _jumpBuffer = 0f;
            if (canExtraJump)
            {
                _extraJumpsRemaining--;
                ShowDoubleJumpFeedback();
            }
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

    public void EnableDoubleJumpAbility(float duration)
    {
        _doubleJumpUntil = Mathf.Max(_doubleJumpUntil, Time.time + duration);
        if (!_isGrounded)
            _extraJumpsRemaining = Mathf.Max(_extraJumpsRemaining, extraJumpsWithPickup);
    }

    public void ClearDoubleJumpAbility()
    {
        _doubleJumpUntil = 0f;
        _extraJumpsRemaining = 0;
    }

    public void SuppressNextJumpInput()
    {
        _jumpBuffer = 0f;
        _suppressJumpInput = true;
    }

    void ShowDoubleJumpFeedback()
    {
        EndlessRunner2D.TryShowPickupMessage("DOUBLE JUMP");

        var effect = new GameObject("DoubleJumpBurst");
        effect.transform.position = transform.position + Vector3.down * 0.35f;

        var particles = effect.AddComponent<ParticleSystem>();
        var main = particles.main;
        main.duration = 0.18f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.55f, 0.95f, 1f, 0.95f),
            new Color(1f, 1f, 1f, 0.9f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 12;

        var emission = particles.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, 10)
        });

        var shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.28f;
        shape.arc = 180f;
        shape.rotation = new Vector3(0f, 0f, 180f);

        var velocity = particles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.y = new ParticleSystem.MinMaxCurve(-0.8f, -1.8f);

        var colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(0.35f, 0.9f, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.95f, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder = 20;

        particles.Play();
        Destroy(effect, 0.7f);
    }
}

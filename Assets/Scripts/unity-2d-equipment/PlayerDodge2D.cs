using UnityEngine;

/// <summary>
/// Dead Cells-style horizontal roll: burst speed + invulnerability.
/// Requires <see cref="Rigidbody2D"/> and <see cref="PlayerInvincibility"/> on the same object.
/// Runs after <see cref="PlayerMovement2D"/> so roll velocity wins for that window.
/// </summary>
[DefaultExecutionOrder(10)]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerDodge2D : MonoBehaviour
{
    public KeyCode dodgeKey = KeyCode.LeftShift;
    public float rollDuration = 0.22f;
    public float rollSpeed = 14f;
    [Tooltip("Invulnerability; can be longer than roll.")]
    public float invulnerabilityDuration = 0.25f;
    public float cooldown = 0.45f;

    [Tooltip("Match facing logic from combat/movement.")]
    public SpriteRenderer facingVisual;

    Rigidbody2D _rb;
    PlayerInvincibility _invuln;
    float _rollUntil;
    float _cooldownUntil;
    float _facingSign = 1f;

    public bool IsRollActive => Time.time < _rollUntil;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _invuln = GetComponent<PlayerInvincibility>();
        if (_invuln == null)
            _invuln = gameObject.AddComponent<PlayerInvincibility>();
    }

    void Update()
    {
        if (Time.time < _cooldownUntil)
            return;
        if (IsRollActive)
            return;
        if (!Input.GetKeyDown(dodgeKey))
            return;

        _facingSign = PlayerFacing2D.GetFacingSign(transform, facingVisual);
        if (Mathf.Approximately(Mathf.Abs(_facingSign), 0f))
            _facingSign = 1f;

        _rollUntil = Time.time + rollDuration;
        _cooldownUntil = Time.time + cooldown;
        _invuln.AddSeconds(invulnerabilityDuration);
    }

    void FixedUpdate()
    {
        if (!IsRollActive)
            return;
        _rb.velocity = new Vector2(_facingSign * rollSpeed, _rb.velocity.y);
    }

}

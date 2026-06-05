using UnityEngine;

/// <summary>
/// Simple ground chase: moves toward the player when within aggro range.
/// Attach to an enemy with <see cref="Rigidbody2D"/> (Dynamic or Kinematic).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class ChaseEnemy2D : MonoBehaviour
{
    public Transform target;
    public float moveSpeed = 3f;
    public float aggroRadius = 8f;

    Rigidbody2D _rb;

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        TryFindPlayerTarget();
    }

    void FixedUpdate()
    {
        TryFindPlayerTarget();

        if (target == null)
            return;

        Vector2 to = (Vector2)(target.position - transform.position);
        if (to.sqrMagnitude > aggroRadius * aggroRadius)
        {
            _rb.velocity = new Vector2(0f, _rb.velocity.y);
            return;
        }

        float dir = Mathf.Sign(to.x);
        _rb.velocity = new Vector2(dir * moveSpeed, _rb.velocity.y);

        if (!Mathf.Approximately(dir, 0f))
        {
            var s = transform.localScale;
            s.x = Mathf.Abs(s.x) * dir;
            transform.localScale = s;
        }
    }

    void TryFindPlayerTarget()
    {
        if (target != null)
            return;

        var p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
            target = p.transform;
    }
}

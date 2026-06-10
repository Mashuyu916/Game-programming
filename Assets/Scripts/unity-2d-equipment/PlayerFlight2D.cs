using UnityEngine;

[DefaultExecutionOrder(20)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerFlight2D : MonoBehaviour
{
    public float verticalSpeed = 6f;
    public float acceleration = 18f;
    public float minimumY = -6.1f;
    public float maximumY = 4.5f;
    public Vector3 cloudOffset = new Vector3(0f, -0.38f, 0f);
    public float cloudScale = 0.72f;

    Rigidbody2D _rb;
    GameObject _cloud;
    float _flightUntil;
    float _normalGravityScale;

    public bool IsFlying => Time.time < _flightUntil;
    public float FlightTimeRemaining => Mathf.Max(0f, _flightUntil - Time.time);

    void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _normalGravityScale = _rb.gravityScale;
    }

    void FixedUpdate()
    {
        if (!IsFlying)
        {
            if (_cloud != null)
                StopFlight();
            return;
        }

        float input = 0f;
        if (Input.GetKey(KeyCode.W))
            input += 1f;
        if (Input.GetKey(KeyCode.S))
            input -= 1f;

        float targetVelocity = input * verticalSpeed;
        Vector2 velocity = _rb.velocity;
        velocity.x = 0f;
        velocity.y = Mathf.MoveTowards(velocity.y, targetVelocity, acceleration * Time.fixedDeltaTime);

        if (transform.position.y >= maximumY && velocity.y > 0f)
            velocity.y = 0f;
        if (transform.position.y <= minimumY && velocity.y < 0f)
            velocity.y = 0f;

        _rb.velocity = velocity;

        Vector3 position = transform.position;
        position.y = Mathf.Clamp(position.y, minimumY, maximumY);
        transform.position = position;
    }

    void Update()
    {
        if (_cloud == null || !IsFlying)
            return;

        float bob = Mathf.Sin(Time.time * 5f) * 0.04f;
        _cloud.transform.localPosition = cloudOffset + Vector3.up * bob;
    }

    public void EnableFlight(float duration)
    {
        _flightUntil = Mathf.Max(_flightUntil, Time.time + duration);
        _rb.gravityScale = 0f;
        _rb.velocity = Vector2.zero;

        if (_cloud == null)
            CreateCloud();
    }

    public void ClearFlightAbility()
    {
        _flightUntil = 0f;
        StopFlight();
    }

    void CreateCloud()
    {
        Sprite cloudSprite = Resources.Load<Sprite>("CloudFlight");
        if (cloudSprite == null)
        {
            Debug.LogWarning("CloudFlight sprite was not found in Resources.");
            return;
        }

        _cloud = new GameObject("FlightCloud");
        _cloud.transform.SetParent(transform, false);
        _cloud.transform.localPosition = cloudOffset;
        _cloud.transform.localScale = Vector3.one * cloudScale;

        var renderer = _cloud.AddComponent<SpriteRenderer>();
        renderer.sprite = cloudSprite;
        renderer.sortingOrder = 11;
    }

    void StopFlight()
    {
        if (_rb != null)
            _rb.gravityScale = _normalGravityScale;

        if (_cloud != null)
            Destroy(_cloud);
        _cloud = null;
    }

    void OnDisable()
    {
        StopFlight();
    }
}

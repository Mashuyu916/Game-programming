using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class RunnerCoinPickup2D : MonoBehaviour
{
    public int value = 1;
    public float spinSpeed = 120f;
    public float bobHeight = 0.12f;
    public float bobSpeed = 4f;

    Vector3 _startPosition;
    bool _collected;

    void Start()
    {
        _startPosition = transform.localPosition;
    }

    void Update()
    {
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f);
        Vector3 position = _startPosition;
        position.y += Mathf.Sin(Time.time * bobSpeed + transform.GetInstanceID()) * bobHeight;
        transform.localPosition = position;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_collected || !other.CompareTag("Player"))
            return;

        _collected = true;
        EndlessRunner2D.AddCoins(value);
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}

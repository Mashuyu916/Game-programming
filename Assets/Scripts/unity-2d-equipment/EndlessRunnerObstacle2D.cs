using UnityEngine;

[DisallowMultipleComponent]
public class EndlessRunnerObstacle2D : MonoBehaviour
{
    public float damage = 35f;
    public bool restartRunnerWhenNoDamageable = true;

    bool _hasDamagedPlayer;

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("Player"))
            return;

        if (_hasDamagedPlayer)
            return;

        var damageable = collision.gameObject.GetComponent<IDamageable>();
        if (damageable == null)
            damageable = collision.gameObject.GetComponentInParent<IDamageable>();

        if (damageable != null)
        {
            damageable.TakeDamage(damage, gameObject);
            _hasDamagedPlayer = true;
            return;
        }

        if (restartRunnerWhenNoDamageable && EndlessRunner2D.TryRestartActiveRunner("HIT AN OBSTACLE", gameObject))
            _hasDamagedPlayer = true;
    }
}

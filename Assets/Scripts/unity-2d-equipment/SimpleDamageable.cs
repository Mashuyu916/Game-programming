using UnityEngine;

/// <summary>
/// Minimal HP pool for prototyping. Attach to an enemy (or a test cube) on the same
/// GameObject that has the Collider2D your hitbox touches.
/// </summary>
public class SimpleDamageable : MonoBehaviour, IDamageable
{
    public float maxHealth = 30f;
    public float currentHealth;

    void Awake()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(float amount, GameObject source)
    {
        currentHealth -= amount;
        if (currentHealth <= 0f)
            Destroy(gameObject);
    }
}

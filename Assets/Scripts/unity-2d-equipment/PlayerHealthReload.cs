using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Player HP + <see cref="IDamageable"/>. Ignores damage while <see cref="PlayerInvincibility"/> is active.
/// On death reloads a scene (roguelike restart).
/// </summary>
public class PlayerHealthReload : MonoBehaviour, IDamageable
{
    public float maxHealth = 100f;
    public float hitInvulnerabilitySeconds = 0.6f;

    [Tooltip("Empty = reload active scene.")]
    public string sceneToLoad = "";

    float _hp;
    PlayerInvincibility _invuln;

    void Awake()
    {
        _hp = maxHealth;
        _invuln = GetComponent<PlayerInvincibility>();
        if (_invuln == null)
            _invuln = gameObject.AddComponent<PlayerInvincibility>();
    }

    public void TakeDamage(float amount, GameObject source)
    {
        if (_invuln.IsInvincible)
            return;

        _hp -= amount;
        _invuln.AddSeconds(hitInvulnerabilitySeconds);

        if (_hp <= 0f)
        {
            if (string.IsNullOrEmpty(sceneToLoad))
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            else
                SceneManager.LoadScene(sceneToLoad);
        }
    }
}

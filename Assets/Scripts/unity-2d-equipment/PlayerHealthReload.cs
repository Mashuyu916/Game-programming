using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Player health that restarts the active runner or reloads the configured scene on death.
/// </summary>
public class PlayerHealthReload : MonoBehaviour, IDamageable
{
    public float maxHealth = 100f;
    public float hitInvulnerabilitySeconds = 0.6f;

    [Tooltip("Empty = reload active scene.")]
    public string sceneToLoad = "";

    float _hp;
    PlayerInvincibility _invuln;

    public float CurrentHealth => _hp;
    public event Action<float, float, float> Damaged;

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

        _hp = Mathf.Max(0f, _hp - amount);
        _invuln.AddSeconds(hitInvulnerabilitySeconds);
        Damaged?.Invoke(amount, _hp, maxHealth);

        if (_hp > 0f)
            return;

        _invuln.Clear();
        string reason = source != null && source.GetComponent<EndlessRunnerObstacle2D>() != null
            ? "HIT AN OBSTACLE"
            : "FATAL DAMAGE";
        if (EndlessRunner2D.TryRestartActiveRunner(reason, source))
            return;

        _hp = maxHealth;
        if (string.IsNullOrEmpty(sceneToLoad))
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        else
            SceneManager.LoadScene(sceneToLoad);
    }

    public void RestoreFullHealth()
    {
        _hp = maxHealth;
        _invuln.Clear();
    }
}

using UnityEngine;

/// <summary>
/// Tracks a simple invulnerability window (dodge i-frames, hit i-frames).
/// Attach to the Player root.
/// </summary>
public class PlayerInvincibility : MonoBehaviour
{
    float _invulnUntil;

    public bool IsInvincible => Time.time < _invulnUntil;

    public void AddSeconds(float seconds)
    {
        if (seconds <= 0f)
            return;
        _invulnUntil = Mathf.Max(_invulnUntil, Time.time + seconds);
    }
}

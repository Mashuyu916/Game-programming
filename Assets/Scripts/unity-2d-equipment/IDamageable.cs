using UnityEngine;

/// <summary>
/// Implement this on anything that should lose HP when hit by <see cref="HitboxDamage2D"/>.
/// Attach a bridge script to your existing Health component if you cannot edit it.
/// </summary>
public interface IDamageable
{
    void TakeDamage(float amount, GameObject source);
}

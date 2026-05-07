using UnityEngine;

/// <summary>
/// Attach to the Player root (same object as your movement script or its parent).
/// Assign a child transform in front of the character as <see cref="attackPivot"/>.
/// Create Weapon assets via: Right-click in Project → Create → Game → Weapon Data.
/// </summary>
public class PlayerEquipmentCombat : MonoBehaviour
{
    [Header("Equipment")]
    public WeaponData mainWeapon;

    [Tooltip("Empty child in front of the player; hitbox is placed relative to this.")]
    public Transform attackPivot;

    [Header("Input")]
    public KeyCode attackKey = KeyCode.J;

    [Tooltip("Layers that can receive damage (usually Enemies).")]
    public LayerMask hittableLayers;

    [Header("Facing")]
    [Tooltip("If set, flipX drives attack direction when scale sign is unreliable.")]
    public SpriteRenderer facingVisual;

    float _cooldownUntil;

    void Update()
    {
        if (mainWeapon == null || attackPivot == null)
            return;

        if (Time.time < _cooldownUntil)
            return;

        if (!Input.GetKeyDown(attackKey))
            return;

        SpawnHitbox(mainWeapon);
        _cooldownUntil = Time.time + mainWeapon.cooldown;
    }

    void SpawnHitbox(WeaponData weapon)
    {
        float facing = GetFacingSign();

        Vector3 local = new Vector3(weapon.hitboxOffset.x * facing, weapon.hitboxOffset.y, 0f);
        Vector3 worldPos = attackPivot.TransformPoint(local);

        var hitboxGo = new GameObject("AttackHitbox");
        hitboxGo.transform.position = worldPos;
        hitboxGo.transform.rotation = attackPivot.rotation;

        var rb = hitboxGo.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeAll;

        var box = hitboxGo.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        box.size = weapon.hitboxSize;

        var dmg = hitboxGo.AddComponent<HitboxDamage2D>();
        dmg.Initialize(weapon.damage, gameObject, hittableLayers, weapon.hitboxDuration);
    }

    float GetFacingSign()
    {
        if (facingVisual != null)
            return facingVisual.flipX ? -1f : 1f;

        float sx = transform.lossyScale.x;
        if (Mathf.Approximately(sx, 0f))
            return 1f;
        return Mathf.Sign(sx);
    }
}

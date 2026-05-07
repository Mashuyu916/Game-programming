using UnityEngine;

[CreateAssetMenu(fileName = "Weapon", menuName = "Game/Weapon Data", order = 0)]
public class WeaponData : ScriptableObject
{
    [Tooltip("For UI / debugging only.")]
    public string displayName = "Sword";

    [Min(0f)]
    public float damage = 10f;

    [Tooltip("How long the hitbox stays active.")]
    [Min(0.01f)]
    public float hitboxDuration = 0.15f;

    [Tooltip("Time after starting an attack before another attack is allowed.")]
    [Min(0f)]
    public float cooldown = 0.35f;

    public Vector2 hitboxSize = new Vector2(1.2f, 0.55f);

    [Tooltip("Offset from the attack pivot in local space. X is mirrored when the player faces left.")]
    public Vector2 hitboxOffset = new Vector2(0.55f, 0.05f);
}

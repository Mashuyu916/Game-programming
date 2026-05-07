using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Put this on a trigger Collider2D. Usually spawned at runtime by <see cref="PlayerEquipmentCombat"/>.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class HitboxDamage2D : MonoBehaviour
{
    float _damage;
    GameObject _owner;
    LayerMask _hittableLayers;
    readonly HashSet<int> _instanceIdsHit = new HashSet<int>();

    /// <summary>
    /// Call right after AddComponent (before physics step runs).
    /// </summary>
    public void Initialize(float damage, GameObject owner, LayerMask hittableLayers, float lifetime)
    {
        _damage = damage;
        _owner = owner;
        _hittableLayers = hittableLayers;
        Destroy(gameObject, lifetime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_owner != null)
        {
            Transform otherRoot = other.transform.root;
            Transform ownerRoot = _owner.transform.root;
            if (otherRoot == ownerRoot)
                return;
        }

        if (((1 << other.gameObject.layer) & _hittableLayers) == 0)
            return;

        int id = other.gameObject.GetInstanceID();
        if (!_instanceIdsHit.Add(id))
            return;

        if (other.TryGetComponent(out IDamageable dmg))
        {
            dmg.TakeDamage(_damage, _owner);
            return;
        }

        dmg = other.GetComponentInParent<IDamageable>();
        if (dmg != null)
            dmg.TakeDamage(_damage, _owner);
    }
}

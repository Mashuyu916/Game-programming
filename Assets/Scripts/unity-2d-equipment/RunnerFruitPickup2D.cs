using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class RunnerFruitPickup2D : MonoBehaviour
{
    public enum FruitType
    {
        Heal,
        DoubleJump,
        Roll
    }

    public FruitType type;
    public float healAmount = 30f;
    public float abilityDuration = 14f;

    bool _collected;

    void OnTriggerEnter2D(Collider2D other)
    {
        if (_collected || !other.CompareTag("Player"))
            return;

        _collected = true;
        string message;

        switch (type)
        {
            case FruitType.DoubleJump:
                var movement = other.GetComponentInParent<PlayerMovement2D>();
                if (movement != null)
                    movement.EnableDoubleJumpAbility(abilityDuration);
                message = "DOUBLE JUMP  " + Mathf.CeilToInt(abilityDuration) + "s";
                break;

            case FruitType.Roll:
                var dodge = other.GetComponentInParent<PlayerDodge2D>();
                if (dodge != null)
                    dodge.EnableRollAbility(abilityDuration);
                message = "ROLL  " + Mathf.CeilToInt(abilityDuration) + "s";
                break;

            default:
                var health = other.GetComponentInParent<PlayerHealthReload>();
                if (health != null)
                    health.Heal(healAmount);
                message = "HEAL +" + Mathf.CeilToInt(healAmount);
                break;
        }

        EndlessRunner2D.TryShowPickupMessage(message);
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}

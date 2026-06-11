using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class RunnerFruitPickup2D : MonoBehaviour
{
    public enum FruitType
    {
        Heal,
        DoubleJump,
        Flight
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

            case FruitType.Flight:
                var flight = other.GetComponentInParent<PlayerFlight2D>();
                if (flight != null)
                    flight.EnableFlight(abilityDuration);
                message = "FLIGHT  " + Mathf.CeilToInt(abilityDuration) + "s  W / S";
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

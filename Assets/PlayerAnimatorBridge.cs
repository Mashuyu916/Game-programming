using UnityEngine;

public class PlayerAnimatorBridge : MonoBehaviour
{
    [Header("References")]
    public Animator animator;
    public Rigidbody2D rb;

    [Header("Input (match your existing scripts)")]
    public KeyCode attackKey = KeyCode.J;
    public KeyCode dodgeKey = KeyCode.LeftShift;

    [Header("Animator Parameters (must match your Animator)")]
    public string speedParam = "velocityX";
    public string attackTriggerParam = "attack";
    [Tooltip("Small damping keeps Idle / Run changes from looking abrupt.")]
    public float speedDampTime = 0.08f;
    [Tooltip("Keeps the running animation active while an endless-runner level scrolls.")]
    public bool endlessRunnerMode;
    public float endlessRunnerVisualSpeed = 7f;

    PlayerFlight2D _flight;

    void Reset()
    {
        rb = GetComponent<Rigidbody2D>();
        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    void Awake()
    {
        if (rb == null) rb = GetComponent<Rigidbody2D>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        _flight = GetComponent<PlayerFlight2D>();

        speedParam = ResolveParameter(speedParam, AnimatorControllerParameterType.Float, "velocityX", "Speed");
        attackTriggerParam = ResolveParameter(attackTriggerParam, AnimatorControllerParameterType.Trigger, "attack", "Attack");
    }

    void Update()
    {
        if (animator == null || rb == null)
            return;

        if (_flight == null)
            _flight = GetComponent<PlayerFlight2D>();

        // 设置横向速度，驱动 Idle / Run 动画
        float visualSpeed = _flight != null && _flight.IsFlying
            ? 0f
            : endlessRunnerMode ? endlessRunnerVisualSpeed : Mathf.Abs(rb.velocity.x);
        animator.SetFloat(speedParam, visualSpeed, speedDampTime, Time.deltaTime);
    }

    public void TriggerAttack()
    {
        if (animator == null)
            return;

        animator.SetTrigger(attackTriggerParam);
    }

    public void ForceIdle()
    {
        endlessRunnerVisualSpeed = 0f;
        if (animator == null)
            return;

        animator.SetFloat(speedParam, 0f);
        animator.Play("Base Layer.Player_Idle", 0, 0f);
        animator.Update(0f);
    }

    string ResolveParameter(string configuredName, AnimatorControllerParameterType type, params string[] fallbacks)
    {
        if (animator == null)
            return configuredName;

        if (HasParameter(configuredName, type))
            return configuredName;

        foreach (string fallback in fallbacks)
        {
            if (HasParameter(fallback, type))
                return fallback;
        }

        return configuredName;
    }

    bool HasParameter(string parameterName, AnimatorControllerParameterType type)
    {
        if (string.IsNullOrEmpty(parameterName))
            return false;

        foreach (var parameter in animator.parameters)
        {
            if (parameter.type == type && parameter.name == parameterName)
                return true;
        }

        return false;
    }
}

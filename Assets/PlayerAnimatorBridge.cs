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
    public string speedParam = "Speed";
    public string attackTriggerParam = "Attack";
    [Tooltip("Small damping keeps Idle / Run changes from looking abrupt.")]
    public float speedDampTime = 0.08f;
    [Tooltip("Keeps the running animation active while an endless-runner level scrolls.")]
    public bool endlessRunnerMode;
    public float endlessRunnerVisualSpeed = 7f;

    public bool enableRollTrigger = true;
    public string rollTriggerParam = "Roll";

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
    }

    void Update()
    {
        if (animator == null || rb == null)
            return;

        // 设置横向速度，驱动 Idle / Run 动画
        float visualSpeed = endlessRunnerMode ? endlessRunnerVisualSpeed : Mathf.Abs(rb.velocity.x);
        animator.SetFloat(speedParam, visualSpeed, speedDampTime, Time.deltaTime);
    }

    public void TriggerAttack()
    {
        if (animator == null)
            return;

        animator.SetTrigger(attackTriggerParam);
    }

    public void TriggerRoll()
    {
        if (!enableRollTrigger || animator == null)
            return;

        animator.SetTrigger(rollTriggerParam);
    }
}

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

        // 用水平速度驱动 Idle/Run
        animator.SetFloat(speedParam, Mathf.Abs(rb.velocity.x));

        // 按键触发攻击动画（Animator 里 Any State -> Attack 的 Trigger）
        if (Input.GetKeyDown(attackKey))
            animator.SetTrigger(attackTriggerParam);

        // 可选：按键触发翻滚动画（如果你做了 Roll 状态/Trigger）
        if (enableRollTrigger && Input.GetKeyDown(dodgeKey))
            animator.SetTrigger(rollTriggerParam);
    }
}
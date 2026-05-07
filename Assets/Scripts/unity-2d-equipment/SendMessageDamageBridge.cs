using UnityEngine;

/// <summary>
/// If your template's Health script cannot implement <see cref="IDamageable"/>,
/// add this bridge and wire the component reference in the Inspector.
/// It forwards float damage to a method name on that Behaviour (default: "TakeDamage").
/// </summary>
public class SendMessageDamageBridge : MonoBehaviour, IDamageable
{
    public Behaviour target;
    public string messageName = "TakeDamage";

    public void TakeDamage(float amount, GameObject source)
    {
        if (target == null)
            return;
        target.SendMessage(messageName, amount, SendMessageOptions.DontRequireReceiver);
    }
}

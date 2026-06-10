using UnityEngine;

// Kept as an empty compatibility component so older scenes do not get a Missing Script.
[DisallowMultipleComponent]
public class PlayerDodge2D : MonoBehaviour
{
    public bool IsRollActive => false;

    void Awake()
    {
        enabled = false;
    }

    public void ClearRollAbility()
    {
    }
}

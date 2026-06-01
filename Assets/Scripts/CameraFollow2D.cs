using UnityEngine;

/// <summary>
/// Keeps the camera centered on the player in 2D. Attach to Main Camera and assign Target.
/// </summary>
public class CameraFollow2D : MonoBehaviour
{
    [Tooltip("Usually the Player root transform.")]
    public Transform target;

    [Tooltip("Camera stays this far behind the target on Z.")]
    public Vector3 offset = new Vector3(0f, 0f, -10f);

    [Tooltip("0 = snap instantly. Higher = smoother follow.")]
    [Min(0f)]
    public float smoothSpeed = 0f;
    public bool lockHorizontal;

    void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 desired = target.position + offset;
        if (lockHorizontal)
            desired.x = transform.position.x;

        if (smoothSpeed <= 0f)
            transform.position = desired;
        else
            transform.position = Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime);
    }
}

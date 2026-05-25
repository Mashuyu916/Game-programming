using UnityEngine;

/// <summary>
/// Place this on an empty object where the player should start (on the grass).
/// Drag that object into Spawn Point, or leave empty to use this transform.
/// </summary>
[DefaultExecutionOrder(-95)]
public class PlayerSpawnPoint2D : MonoBehaviour
{
    [Tooltip("Where the Player should appear. If empty, uses this object's transform.")]
    public Transform spawnPoint;

    [Tooltip("Small upward offset so the collider clears the ground on the first frame.")]
    public float spawnUpOffset = 0.05f;

    void Start()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return;

        Transform point = spawnPoint != null ? spawnPoint : transform;
        var rb = player.GetComponent<Rigidbody2D>();

        Vector3 pos = point.position;
        pos.y += spawnUpOffset;
        pos.z = player.transform.position.z;

        player.transform.position = pos;
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.Sleep();
            rb.WakeUp();
        }
    }

    void OnDrawGizmos()
    {
        Transform point = spawnPoint != null ? spawnPoint : transform;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(point.position, 0.25f);
        Gizmos.DrawLine(point.position, point.position + Vector3.up * 0.6f);
    }
}

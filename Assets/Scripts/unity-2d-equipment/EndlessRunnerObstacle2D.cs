using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class EndlessRunnerObstacle2D : MonoBehaviour
{
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}

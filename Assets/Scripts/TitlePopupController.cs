using UnityEngine;
using UnityEngine.SceneManagement;

public class TitlePopupController : MonoBehaviour
{
    [Tooltip("Scene name to load when Start is pressed.")]
    public string sceneToLoad = "1";
    [Tooltip("If >=0, load scene by build index instead of name.")]
    public int sceneBuildIndex = 1;

    public void StartGame()
    {
        if (sceneBuildIndex >= 0)
        {
            SceneManager.LoadScene(sceneBuildIndex);
            return;
        }

        if (string.IsNullOrEmpty(sceneToLoad))
        {
            Debug.LogWarning("TitlePopupController: sceneToLoad is empty and no build index set.");
            return;
        }
        SceneManager.LoadScene(sceneToLoad);
    }

    public void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}

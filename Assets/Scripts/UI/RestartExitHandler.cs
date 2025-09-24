using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class RestartExitHandler : MonoBehaviour
{
    [Tooltip("Optional: If empty, reloads the current active scene.")]
    [SerializeField] private string sceneToLoad;

    [Header("UI Panels")]
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private GameObject defeatPanel;

    private void Update()
    {
        if (!IsActive()) return; // only listen when UI is visible

        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.eKey.wasPressedThisFrame)
        {
            RestartGame();
        }

        if (kb.qKey.wasPressedThisFrame)
        {
            ExitGame();
        }
    }

    private bool IsActive()
    {
        return (victoryPanel != null && victoryPanel.activeInHierarchy) ||
               (defeatPanel != null && defeatPanel.activeInHierarchy);
    }

    private void RestartGame()
    {
        string target = string.IsNullOrEmpty(sceneToLoad)
            ? SceneManager.GetActiveScene().name
            : sceneToLoad;

        SceneManager.LoadScene(target);
    }

    private void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}

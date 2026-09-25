using UnityEngine;
using UnityEngine.SceneManagement;

// Scene changes that a UI button can call directly. Battle's exit button uses
// LoadMainMenu; the menu's own Sair still quits the application.
//
// The menu is build index 0 and battle is 1, but this looks the menu up by name
// so reordering the build settings can't silently send the player somewhere else.
public class SceneNavigator : MonoBehaviour
{
    [Tooltip("Scene loaded when leaving a battle.")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    public void LoadMainMenu()
    {
        Time.timeScale = 1f;   // in case we are leaving from a paused screen
        SceneManager.LoadScene(mainMenuSceneName);
    }

    public void QuitGame()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}

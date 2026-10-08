using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenu : MonoBehaviour
{
    private StartMenu _startMenu;

    private int BattleSceneIndex
    {
        get { return SceneManager.GetActiveScene().buildIndex + 1; }
    }

    // Jogar opens the start menu: continue the latest save, or a new game in
    // the direction the player picks (Portuguese to Libras or back).
    public void PlayGame()
    {
        // A plain new battle - drop any save that was queued and never loaded.
        SaveSystem.ConsumePending();

        if (_startMenu != null) return;
        _startMenu = StartMenu.Show(FindPlayButton(), ContinueRun, StartRun);
        if (_startMenu == null) StartRun(SignProgression.CurrentDirection);
    }

    private void ContinueRun(SaveData save)
    {
        SaveSystem.LoadIntoBattle(save, BattleSceneIndex);
    }

    private void StartRun(SignProgression.Direction direction)
    {
        // The same direction as last time carries on with the words already
        // learned, as it always has. Switching direction starts the words over,
        // since "learned" means the opposite thing in the other one.
        if (direction != SignProgression.CurrentDirection)
            SignProgression.ResetAll(direction);

        SceneManager.LoadScene(BattleSceneIndex);
    }

    // The start menu borrows the Jogar button's look; find it by what it calls
    // rather than by name, so renaming it in the scene can't break this.
    private Button FindPlayButton()
    {
        foreach (var b in FindObjectsOfType<Button>(true))
            for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
                if (b.onClick.GetPersistentTarget(i) == this && b.onClick.GetPersistentMethodName(i) == "PlayGame")
                    return b;
        return null;
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}

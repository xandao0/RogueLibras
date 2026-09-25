using System.Collections;
using UnityEngine;

// Lets go of a save picked in the menu (or from Recomeçar) once the battle scene
// has finished setting itself up from it.
//
// The managers read SaveSystem.Pending themselves during Awake/Start -
// CombatManager for health, CardManager for the deck, EnemyManager for the
// difficulty and the enemy being fought - so the first hand is already dealt
// from the saved deck. This only has to clear it afterwards, so the next enemy
// of the run is a fresh one rather than the saved one again.
public class PendingSaveLoader : MonoBehaviour
{
    private IEnumerator Start()
    {
        if (!SaveSystem.HasPending) yield break;

        // One frame on, every Start in the scene has run.
        yield return null;
        SaveSystem.ConsumePending();

        if (UIManager.instance != null) UIManager.instance.UpdateDisplay();
    }
}

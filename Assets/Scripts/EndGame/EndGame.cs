using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class EndGame : MonoBehaviour
{
    public void PlayGame() 
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex - 1);
    }
    
    public void QuitGame()
    {
        Application.Quit();
    }
    private void OnEnable()
    {
        GameManager.ChangeState(GameStates.ENDMATCH);
        CardManager.instance.endTurnButton.interactable = false;
        
        for (int i = CardManager.instance.cardHolderContainer.childCount - 1; i >= 0; i--)
        {
                 
            CardManager.instance.DiscardCard(CardManager.instance.cardHolderContainer.GetChild(i).GetComponent<CardDisplay>());
                     
        }
    }
    
    
}

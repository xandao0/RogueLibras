using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EndMatch : MonoBehaviour
{
    public Transform cardContainer;

    private void OnEnable()
    {
        GameManager.ChangeState(GameStates.ENDMATCH);
        CardManager.instance.endTurnButton.interactable = false;
        
        List<CardDataSo> allCards = new List<CardDataSo>();
        allCards.AddRange(CardManager.instance.allCardsThatExist);

        for (int i = 0; i < 3; i++)
        {
            GameObject g = Instantiate(CardManager.instance.newCardPrefab, cardContainer);
            int r = Random.Range(7, allCards.Count);
            g.GetComponent<CardDisplay>().card = allCards[r];
            allCards.RemoveAt(r);
        }
        
        for (int i = CardManager.instance.cardHolderContainer.childCount - 1; i >= 0; i--)
        {
                 
            CardManager.instance.DiscardCard(CardManager.instance.cardHolderContainer.GetChild(i).GetComponent<CardDisplay>());
                     
        }
    }

    private void OnDisable()
    {
        for (int i = cardContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(cardContainer.GetChild(i).gameObject);
        }
        
        GameManager.ChangeState(GameStates.COMBAT);
    }
}

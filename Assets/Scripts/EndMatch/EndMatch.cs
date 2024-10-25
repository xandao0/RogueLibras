using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EndMatch : MonoBehaviour
{
    public Transform cardContainer;

    private void OnEnable()
    {
        GameManager.ChangeState(GameStates.ENDMATCH);

        for (int i = CardManager.instance.cardHolderContainer.childCount - 1; i >= 0; i--)
        {
            CardManager.instance.DiscardCard(CardManager.instance.cardHolderContainer.GetChild(i).GetComponent<CardDisplay>());
        }

        List<CardDataSo> allCards = new List<CardDataSo>();
        allCards.AddRange(CardManager.instance.allCardsThatExist);

        for (int i = 0; i < 3; i++)
        {
            GameObject g = Instantiate(CardManager.instance.newCardPrefab, cardContainer);
            int r = Random.Range(0, allCards.Count);
            g.GetComponent<CardDisplay>().card = allCards[r];
            allCards.RemoveAt(r);
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

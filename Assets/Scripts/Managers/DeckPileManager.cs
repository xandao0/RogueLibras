using System.Collections;
using UnityEngine;

public class DeckPileManager : MonoBehaviour
{
    public GameObject newCardPrefab;

    [Header("Containers")]
    public Transform drawContainer;
    public Transform discardContainer;
    public Transform cardHolderContainer;

    public void InstantiateCard(CardDataSo cardData)
    {
        GameObject g = Instantiate(newCardPrefab, drawContainer);
        CardDisplay display = g.GetComponent<CardDisplay>();
        display.card = cardData;
        g.name = cardData.cardName;
    }

    public void MoveRandomToHand()
    {
        if (drawContainer.childCount <= 0) return;

        int random = Random.Range(0, drawContainer.childCount);
        Transform cardToDraw = drawContainer.GetChild(random);
        cardToDraw.SetParent(cardHolderContainer);
    }

    public void ReshuffleDiscardToDraw()
    {
        for (int i = discardContainer.childCount - 1; i >= 0; i--)
        {
            Transform tempCard = discardContainer.GetChild(i);
            tempCard.SetParent(drawContainer);
            ResetCardTransform(tempCard);
        }
    }

    public void DiscardCard(CardDisplay c)
    {
        for (int i = 0; i < cardHolderContainer.childCount; i++)
        {
            if (cardHolderContainer.GetChild(i).GetComponent<CardDisplay>() == c)
            {
                Transform temp = cardHolderContainer.GetChild(i);
                temp.SetParent(discardContainer);
                ResetCardTransform(temp);
                return;
            }
        }
    }

    public void ResetCardTransform(Transform card)
    {
        card.localPosition = Vector2.zero;
    }

    public IEnumerator ClearContainers()
    {
        for (int i = drawContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(drawContainer.GetChild(i).gameObject);
            yield return null;
        }

        for (int i = discardContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(discardContainer.GetChild(i).gameObject);
            yield return null;
        }
    }
}
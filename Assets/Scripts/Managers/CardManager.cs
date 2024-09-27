using System.Collections;
using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;


public class CardManager : MonoBehaviour
{
    public static CardManager instance;

    #region Variables
    public CurrentTurn currentTurn;

    public GameObject newCardPrefab;

    public Transform drawContainer; //cards will sit inside this object when in draw pile
    public Transform discardContainer; //cards will sit inside this object when in discard pile
    public Transform cardHolderContainer;//spot on the screen that shows current cards in your hand
    
    [Header("Cards In Current Deck")]
    //all cards that you currently have in your deck
    public List<CardDataSo> currentAvailableCards = new List<CardDataSo>();
    [Header("All Cards In Game")]
    //all cards can exist in the game (place any newly created cardDataSO in this list)
    public List<CardDataSo> allCardsThatExist = new List<CardDataSo>();
    
    [Header("Hand Size")]
    //amount of cards drawn at start of turn
    public int startingHandSize = 5;
    //cardHolderContainer can not have more than maxHandSize as the child objects
    public int maxHandSize = 10;

    [Header("Stamina")]
    //stamina at the start of every turn
    public int staminaAtStart = 3;
    //your current stamina left for current turn
    public int currentStamina = 3;

    public bool isStartingDraw;
    
    [Header("UI")]
    //show how many cards left in draw pile
    public TMP_Text drawPileText;
    //show how many cards in discard pile
    public TMP_Text discardPileText;
    //reference to the button that ends your current turn
    public Button endTurnButton;
    #endregion
    private void Awake()
    {
        //if a new scene starts and an object exists already with this script on it, then destroy that object
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        instance = this;
    }

    private void Start()
    {
        isStartingDraw = true;
        LoadDeck();
    }

    //Happens at the beginning of combat (each enemy)
    public void LoadDeck()
    {
        for (int i = 0; i < currentAvailableCards.Count; i++)
        {
            GameObject g = Instantiate(newCardPrefab, drawContainer);
            //set the Card to the CardData for the cloned prefab
            //set the card's name in hierarchy
            g.GetComponent<CardDisplay>().card = currentAvailableCards[i];
            g.name = g.GetComponent<CardDisplay>().card.cardName;
        }

        UpdateDisplay();
        EnemyManager.instance.SpawnEnemy();

        InitialDrawForTurn();
    }

    public void UpdateDisplay()
    {
        drawPileText.text = drawContainer.childCount.ToString();
        discardPileText.text = discardContainer.childCount.ToString();

        for (int i = 0; i < cardHolderContainer.childCount; i++)
        {
            //check if you can use each card in your hand and change the color of the stamina cost accordingly
            CardDisplay c = cardHolderContainer.transform.GetChild(i).GetComponent<CardDisplay>();

            if (CanUseCard(c))
            {
                c.cardStaminaText.color = Color.blue;
            }
            else
            {
                c.cardStaminaText.color = Color.red;
            }
        }
    }
    
    //first time we draw cards each turn
    public void InitialDrawForTurn()
    {
        currentTurn = CurrentTurn.PLAYERTURN;

        endTurnButton.interactable = true;
        currentStamina = staminaAtStart;
        CombatManager.instance.currentEnemy.OnNewTurn();
        
        //if the starting hand size is larger than the current hand, then draw a card. Otherwise, don't
        if (cardHolderContainer.childCount < startingHandSize)
        {
            DrawCard();
        }
        else
        {
            isStartingDraw = false;
        }
        
        UIManager.instance.UpdateDisplay();
        UpdateDisplay();
    }

    public void DrawCard()
    {
        //amount of cards in draw pile
        if (drawContainer.childCount > 0)
        {
            //draw a card
            int random = Random.Range(0, drawContainer.childCount);
            Transform cardToDraw = drawContainer.GetChild(random);
            cardToDraw.SetParent(cardHolderContainer);
            //drawContainer.GetChild(r).transform.parent = cardHolderContainer;

        }
        else if (drawContainer.childCount <= 0)
        {
            ReshuffleDeck();
        }

        if (isStartingDraw)
        {
            InitialDrawForTurn();
        }
    }

    public IEnumerator DrawCards(CardDisplay card)
    {
        int amount = card.cardDraw;

        for (int i = 0; i < amount; i++)
        {
            DrawCard();
            yield return new WaitForSeconds(0.02f);
        }

        yield return new WaitForEndOfFrame();

        DiscardCard(card);
        
        UpdateDisplay();
    }

    public void ReshuffleDeck()
    {
        for (int i = discardContainer.childCount - 1; i >= 0; i--)
        {
            Transform tempCard = discardContainer.GetChild(i);

            //tempCard.transform.parent = drawContainer;
            tempCard.transform.SetParent(drawContainer);
            ResetCardTransform(tempCard);
        }
        
        UpdateDisplay();
    }

    public void DiscardCard(CardDisplay c)
    {
        for (int i = 0; i < cardHolderContainer.childCount; i++)
        {
            if (cardHolderContainer.GetChild(i).GetComponent<CardDisplay>() == c)
            {
                Transform temp = cardHolderContainer.GetChild(i);

                temp.transform.parent = discardContainer;
                ResetCardTransform(temp);
                UpdateDisplay();

                return;
            }
        }
    }

    public void ResetCardTransform(Transform card)
    {
        card.localPosition = Vector2.zero;
    }
    
    public bool CanUseCard(CardDisplay c)
    {
        return currentStamina > 0 && c.cardStamina <= currentStamina;
    }

    public void StartNewTurn()
    {
        CombatManager.instance.currentBlock = 0;

        isStartingDraw = true;
        InitialDrawForTurn();
    }

    public void EndTurn()
    {
        for (int i = cardHolderContainer.childCount - 1; i >= 0; i--)
        {
            DiscardCard(cardHolderContainer.GetChild(i).GetComponent<CardDisplay>());
        }

        endTurnButton.interactable = false;
        currentTurn = CurrentTurn.ENEMYTURN;
        CombatManager.instance.currentEnemy.blockedDemage = 0;

        StartCoroutine(EnemyManager.instance.TakeEnemyTurn(CombatManager.instance.currentEnemy));
        UpdateDisplay();
    }
}

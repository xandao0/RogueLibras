using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardManager : MonoBehaviour
{
    public static CardManager instance;

    [Header("Sub-Gerenciadores")]
    public StaminaManager staminaManager;
    public DeckPileManager pileManager;

    [Header("Turn & Hand Config")]
    public CurrentTurn currentTurn;
    public bool isStartingDraw;
    public int startingHandSize = 5;
    public int maxHandSize = 10;

    [Header("Card Lists")]
    public List<CardDataSo> currentAvailableCards = new List<CardDataSo>();
    public List<CardDataSo> allCardsThatExist = new List<CardDataSo>();

    [Header("UI References")]
    public TMP_Text drawPileText;
    public TMP_Text discardPileText;
    public Button endTurnButton;
    
    public int currentStamina
    {
        get => staminaManager.currentStamina;
        set => staminaManager.currentStamina = value;
    }

    public int staminaAtStart
    {
        get => staminaManager.staminaAtStart;
        set => staminaManager.staminaAtStart = value;
    }

    public Transform drawContainer => pileManager.drawContainer;
    public Transform discardContainer => pileManager.discardContainer;
    public Transform cardHolderContainer => pileManager.cardHolderContainer;
    public GameObject newCardPrefab => pileManager.newCardPrefab;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    private void Start()
    {
        isStartingDraw = true;
        LoadDeck();
    }

    public IEnumerator ResetCombatWithNewDeck()
    {
        yield return pileManager.ClearContainers();

        int r = EnemyManager.instance.GetRandomEnemyIndex();
        UIManager.instance.endMatchGO.SetActive(false);

        isStartingDraw = true;
        LoadDeck();
    }

    private void LoadDeck()
    {
        for (int i = 0; i < currentAvailableCards.Count; i++)
        {
            pileManager.InstantiateCard(currentAvailableCards[i]);
        }

        UpdateDisplay();
        EnemyManager.instance.SpawnEnemy();
        InitialDrawForTurn();
    }

    public void UpdateDisplay()
    {
        drawPileText.text = pileManager.drawContainer.childCount.ToString();
        discardPileText.text = pileManager.discardContainer.childCount.ToString();

        for (int i = 0; i < pileManager.cardHolderContainer.childCount; i++)
        {
            CardDisplay c = pileManager.cardHolderContainer.GetChild(i).GetComponent<CardDisplay>();
            c.cardStaminaText.color = CanUseCard(c) ? Color.blue : Color.white;
        }
    }

    private void InitialDrawForTurn()
    {
        currentTurn = CurrentTurn.PLAYERTURN;
        endTurnButton.interactable = true;
        staminaManager.ResetStamina();
        CombatManager.instance.currentEnemy.OnNewTurn();

        if (pileManager.cardHolderContainer.childCount < startingHandSize)
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

    private void DrawCard()
    {
        if (pileManager.drawContainer.childCount > 0)
        {
            pileManager.MoveRandomToHand();
        }
        else
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
        pileManager.ReshuffleDiscardToDraw();
        UpdateDisplay();
    }

    public void DiscardCard(CardDisplay c)
    {
        pileManager.DiscardCard(c);
        UpdateDisplay();
    }

    public bool CanUseCard(CardDisplay c)
    {
        return staminaManager.HasEnoughStamina(c.cardStamina);
    }

    public void StartNewTurn()
    {
        CombatManager.instance.currentBlock = 0;
        isStartingDraw = true;
        InitialDrawForTurn();
    }

    public void EndTurn()
    {
        for (int i = pileManager.cardHolderContainer.childCount - 1; i >= 0; i--)
        {
            DiscardCard(pileManager.cardHolderContainer.GetChild(i).GetComponent<CardDisplay>());
        }

        endTurnButton.interactable = false;
        currentTurn = CurrentTurn.ENEMYTURN;
        CombatManager.instance.currentEnemy.blockedDemage = 0;

        StartCoroutine(EnemyManager.instance.TakeEnemyTurn(CombatManager.instance.currentEnemy));
        UpdateDisplay();
    }
}
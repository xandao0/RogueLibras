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

    [Header("Selection")]
    //the card currently raised/enlarged in hand, awaiting a double-click to be used
    public CardDisplay selectedCard;

    [Header("VFX")]
    //plays a brief "burning card" flash on the discard pile counter each discard
    public DiscardBurnEffect discardBurnEffect;
    //RogueLibras/UI Fire material, used for the "card burns away where it sat" effect
    public Material cardBurnMaterial;
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
        // GameManager outlives scenes, so a battle can inherit ENDMATCH from the
        // last one - where clicking a card claims it as a reward.
        if (GameManager.instance != null) GameManager.ChangeState(GameStates.COMBAT);

        // Loading a save: deal the first hand from the saved deck.
        SaveData save = SaveSystem.Pending;
        if (save != null)
        {
            var deck = SaveSystem.ResolveDeck(save.deckCardNames, allCardsThatExist);
            if (deck.Count > 0) currentAvailableCards = deck;
        }

        isStartingDraw = true;
        LoadDeck();
    }

    public IEnumerator ResetCombatWithNewDeck()
    {
        yield return null;

        if (drawContainer.childCount > 0)
        {
            for (int i = drawContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(drawContainer.GetChild(i).gameObject);
                yield return null;
            }
        }
        
        if (discardContainer.childCount > 0)
        {
            for (int i = discardContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(discardContainer.GetChild(i).gameObject);
                yield return null;
            }
        }

        int r = EnemyManager.instance.GetRandomEnemyIndex();
       
        /*
        if (EffectsManager.instance.playerStatusContainer.childCount > 0)
        {
            for (int i = CombatManager.instance.currentStatusEffects.Count - 1; i >= 0; i--)
            {
                EffectsManager.instance.RemoveStatus(EffectsManager.instance.playerStatusContainer.GetChild(i).gameObject);
                CombatManager.instance.currentStatusEffectsLengths.RemoveAt(i);
                CombatManager.instance.currentStatusEffects.RemoveAt(i);
            }
        }
        */
        
        UIManager.instance.endMatchGO.SetActive(false);

        isStartingDraw = true;
        LoadDeck();
    }

    //Happens at the beginning of combat (each enemy)
    private void LoadDeck()
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
        // The pile counts are numbers or signs, like every other number - the
        // plain signs, as the card outline smears them at this size.
        SignText.UsePlainSigns(drawPileText);
        SignText.UsePlainSigns(discardPileText);
        drawPileText.text = SignText.Number(drawContainer.childCount, pileSignScale);
        discardPileText.text = SignText.Number(discardContainer.childCount, pileSignScale);

        //defensive: don't hold onto a selection for a card that left the hand
        if (selectedCard != null && selectedCard.transform.parent != cardHolderContainer)
        {
            selectedCard = null;
        }

        for (int i = 0; i < cardHolderContainer.childCount; i++)
        {
            //affordability cue: keep the cost number in the card palette (#441700)
            //and dim the whole energy badge when the card can't be paid for.
            CardDisplay c = cardHolderContainer.transform.GetChild(i).GetComponent<CardDisplay>();

            bool affordable = CanUseCard(c);

            if (c.cardStaminaText != null)
                c.cardStaminaText.color = new Color32(0x44, 0x17, 0x00, 0xFF);

            if (c.energyBadgeImage != null)
                c.energyBadgeImage.color = affordable ? Color.white : new Color(1f, 1f, 1f, 0.4f);
        }
    }
    
    //first time we draw cards each turn
    private void InitialDrawForTurn()
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

    private void DrawCard()
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

    private void ReshuffleDeck()
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

                // A card the player actively used burns away in place; ordinary
                // discards just slide off. The real card still goes straight to
                // the discard pile so the deck stays intact.
                bool played = c != null && c.ConsumePlayedFlag();
                if (played) CardBurnAway.Spawn(c, cardBurnMaterial);

                temp.transform.parent = discardContainer;
                ResetCardTransform(temp);
                UpdateDisplay();

                if (discardBurnEffect != null)
                {
                    if (played) discardBurnEffect.PlayBig();
                    else discardBurnEffect.Play();
                }

                return;
            }
        }
    }

    private void ResetCardTransform(Transform card)
    {
        card.localPosition = Vector2.zero;
        card.localRotation = Quaternion.identity;
        card.localScale = Vector3.one;

        CanvasGroup cg = card.GetComponent<CanvasGroup>();
        if (cg != null) cg.alpha = 1f;

        if (selectedCard != null && selectedCard.transform == card) selectedCard = null;
    }

    //selects/raises a card in hand so it can be confirmed with a double-click;
    //clicking the already-selected card puts it back down
    public void SelectCard(CardDisplay c)
    {
        selectedCard = (selectedCard == c) ? null : c;
    }

    public bool CanUseCard(CardDisplay c)
    {
        return currentStamina > 0 && c.cardStamina <= currentStamina;
    }

    public void StartNewTurn()
    {
        CombatManager.instance.currentBlock = 0;
        //CombatManager.instance.currentEnemy.ReduceStatusEffectsOnNewTurn();

        isStartingDraw = true;
        InitialDrawForTurn();

        // After the hand is dealt, so the word that graduates is one the player
        // can actually see this turn.
        AdvanceSignProgression();
    }

    private readonly List<string> _signWordPool = new List<string>();

    // One word per turn turns into SignWriting (or back into Portuguese), drawn
    // at random from everything readable in the current hand and on the HUD -
    // the pile counts, the block, the enemy's name and numbers. Nothing happens
    // for the first few turns.
    private void AdvanceSignProgression()
    {
        _signWordPool.Clear();
        for (int i = 0; i < cardHolderContainer.childCount; i++)
        {
            CardDisplay c = cardHolderContainer.GetChild(i).GetComponent<CardDisplay>();
            if (c != null) c.CollectSignWords(_signWordPool);
        }

        SignText.NumberWords(drawContainer.childCount, _signWordPool);
        SignText.NumberWords(discardContainer.childCount, _signWordPool);
        List<ISignWordSource> hud = HudSignSources();
        foreach (var source in hud) source.CollectSignWords(_signWordPool);

        string learned = SignProgression.OnTurnStarted(_signWordPool);
        if (string.IsNullOrEmpty(learned)) return;

        UpdateDisplay();
        foreach (var source in hud) source.RefreshSignText();

        // Cards in hand that carry the word change under a shine the player can
        // watch - after the new hand has fanned out, one card after another. The
        // word may be on cards in the piles too; those just switch.
        int revealed = 0;
        foreach (Transform container in new Transform[] { cardHolderContainer, drawContainer, discardContainer })
        {
            if (container == null) continue;
            for (int i = 0; i < container.childCount; i++)
            {
                CardDisplay c = container.GetChild(i).GetComponent<CardDisplay>();
                if (c == null) continue;

                if (container == cardHolderContainer && c.ShowsWord(learned))
                    c.RevealSign(learned, signRevealDelay + signRevealStagger * revealed++);
                else
                    c.RefreshSignText();
            }
        }
    }

    [Header("Sign Reveal")]
    [Tooltip("Seconds after the hand is dealt before a newly learned sign shines in.")]
    [SerializeField] private float signRevealDelay = 0.55f;
    [Tooltip("Extra delay per card when several cards in hand show the new sign.")]
    [SerializeField] private float signRevealStagger = 0.12f;

    [Tooltip("Size of the signs that replace the pile counts, relative to the text.")]
    [Range(0.6f, 3f)] [SerializeField] private float pileSignScale = 1.3f;

    private static List<ISignWordSource> HudSignSources()
    {
        var list = new List<ISignWordSource>();
        foreach (var mb in FindObjectsOfType<MonoBehaviour>())
        {
            var source = mb as ISignWordSource;
            if (source != null) list.Add(source);
        }
        return list;
    }

    /// Redraws the wording on every card in the battle, and on the HUD - after
    /// the sign progression changes underneath them.
    public void RefreshAllCardText()
    {
        UpdateDisplay();
        foreach (var source in HudSignSources()) source.RefreshSignText();

        foreach (Transform container in new Transform[] { cardHolderContainer, drawContainer, discardContainer })
        {
            if (container == null) continue;
            for (int i = 0; i < container.childCount; i++)
            {
                CardDisplay c = container.GetChild(i).GetComponent<CardDisplay>();
                if (c != null) c.RefreshSignText();
            }
        }
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

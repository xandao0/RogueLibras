using System.Collections;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

public class CardDisplay : MonoBehaviour
{
    public CardDataSo card;
    
    [Header("Card Info")]
    public TMP_Text cardNameText;
    public TMP_Text cardDescriptionText;
    
    //variables to the video
    public RawImage videoDisplay;
    public VideoPlayer videoCard;
    
    public TMP_Text cardTypeText;
    public TMP_Text cardStaminaText;

    [Header("Card Data")] 
    public string cardName;
    public string cardDesc;
    public CardTypes cardType;
    public int cardStamina;

    public int strength;
    public int defense;
    public int cardDraw;
    public int cure;

    [SerializeField] private AudioClip _attackSFX;
    [SerializeField] private AudioClip _defenseSFX;
    [SerializeField] private AudioClip _itemSFX;
    [SerializeField] private GameObject _particle;

    private AudioSource _audioSource;
    
    private RenderTexture uniqueRenderTexture;
    
    // Start is called before the first frame update
    private void Start()
    {
        CollectInfoFromCardSo();
        _audioSource = GetComponent<AudioSource>();
    }

    private void CollectInfoFromCardSo()
    {
        
        if (card == null)
        {
            //Debug.LogError("Card is not assigned!");
            Destroy(gameObject);
            return;
        }
        
        if (card.cardVideo == null)
        {
            Debug.LogWarning("Card video is not assigned!");
        }

        cardName = card.cardName;
        cardDesc = card.cardDescription;
        cardType = card.type;
        cardStamina = card.cardStamina;
        strength = card.strength;
        defense = card.defense;
        cardDraw = card.cardDrawAmount;
        cure = card.cure;

        UpdateDisplay();
    }

    public void UpdateDisplay()
    {
        cardNameText.text = cardName;
        cardDesc = card.cardDescription;
        cardDesc = ProcessDescription();
        cardDescriptionText.text = cardDesc;
        cardTypeText.text = cardType.ToString().ToUpper();
        cardStaminaText.text = cardStamina.ToString();

        CardColor();
        PlayVideo(card.cardVideo);
    }
    
    private void PlayVideo(VideoClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("No video clip assigned to the card.");
            return;
        }

        // Create a unique RenderTexture for this card to avoid shared textures
        uniqueRenderTexture = new RenderTexture(864, 1168, 0);
        videoDisplay.texture = uniqueRenderTexture;
        videoCard.targetTexture = uniqueRenderTexture;

        videoCard.clip = clip;
        videoCard.Prepare();

        StartCoroutine(WaitAndPlay());
        
    }
    
    private IEnumerator WaitAndPlay()
    {
        while (!videoCard.isPrepared)
        {
            yield return null; // Wait until the video is ready
        }

        videoCard.Play();
    }
    
    private void OnDestroy()
    {
        if (uniqueRenderTexture != null)
        {
            uniqueRenderTexture.Release(); // Release the RenderTexture when the card is destroyed
        }
    }
    
    

    private string ProcessDescription()
    {
        string[] temp = cardDesc.Split(' ');
        string newCardDesc = "";
        cardDesc = "";
        
        switch (card.type)
        {
            case CardTypes.ATAQUE:
                
                for (int i = 0; i < temp.Length; i++)
                {
                    if (temp[i].ToUpper() == "S")
                    {
                        switch (GameManager.instance.currentState)
                        {
                            case GameStates.COMBAT:
                                /*
                                if (CombatManager.instance.currentEnemy.currentStatusEffects.Contains(StatusEffects
                                        .VULNERABLE))
                                {
                                    temp[i] = string.Format("<color=#00FF00>{0}</color>",
                                        Mathf.RoundToInt((strength + CombatManager.instance.currentMight) *
                                                         EffectsManager.instance
                                                             .GetStatusEffect(StatusEffects.VULNERABLE)
                                                             .effectStrength));
                                }
                                
                                else if (CombatManager.instance.currentEnemy.currentStatusEffects.Contains(StatusEffects
                                             .WEAK))
                                {
                                    temp[i] = string.Format("<color=#00FF00>{0}</color>",
                                        Mathf.RoundToInt((strength + CombatManager.instance.currentMight) *
                                                         EffectsManager.instance
                                                             .GetStatusEffect(StatusEffects.WEAK)
                                                             .effectStrength));
                                }
                                else temp[i] = strength.ToString();
                                */
                                temp[i] = strength.ToString();
                                break;
                            case GameStates.ENDMATCH:
                                temp[i] = strength.ToString();
                                break;
                        }
                        
                    }
                    
                    newCardDesc += temp[i];
                    if (i < temp.Length - 1)
                        newCardDesc += " ";
                }

                break;            
            case CardTypes.DEFESA:
                for (int i = 0; i < temp.Length; i++)
                {
                    if (temp[i].ToUpper() == "D")
                    {
                        switch (GameManager.instance.currentState)
                        {
                            case GameStates.COMBAT:
                                /*
                                if (CombatManager.instance.currentStatusEffects.Contains(StatusEffects.FRAIL))
                                {
                                    temp[i] = string.Format("<color=#FF0000>{0}</color>",
                                        Mathf.RoundToInt((defense * EffectsManager.instance
                                            .GetStatusEffect(StatusEffects.FRAIL).effectStrength)));
                                }
                                */
                                temp[i] = defense.ToString();
                                break;
                            case GameStates.ENDMATCH:
                                temp[i] = defense.ToString();
                                break;
                        }
                    }
                    if (temp[i].ToUpper() == "C")
                    {
                        temp[i] = cure.ToString();
                    }
                    
                    newCardDesc += temp[i];
                    if (i < temp.Length - 1)
                        newCardDesc += " ";
                }

                break;            
            case CardTypes.ITEM:
                for (int i = 0; i < temp.Length; i++)
                {
                    if (temp[i].ToUpper() == "C")
                    {
                        temp[i] = cure.ToString();
                    }
                    if (temp[i].ToUpper() == "D")
                    {
                        switch (GameManager.instance.currentState)
                        {
                            case GameStates.COMBAT:
                                temp[i] = defense.ToString();
                                break;
                            case GameStates.ENDMATCH:
                                temp[i] = defense.ToString();
                                break;
                        }
                    }
                    
                    newCardDesc += temp[i];
                    if (i < temp.Length - 1)
                        newCardDesc += " ";
                }

                break;
            case CardTypes.BOOST:
                for (int i = 0; i < temp.Length; i++)
                {
                    if (temp[i].ToUpper() == "X")
                    {
                        temp[i] = cardDraw.ToString();
                    }
                    
                    newCardDesc += temp[i];
                    if (i < temp.Length - 1)
                        newCardDesc += " ";
                }
                break;
        }
        
        return newCardDesc;
    }

    private void CardColor()
    {
        switch (card.type)
        {
            case CardTypes.ATAQUE:
                GetComponent<Image>().color = Color.red;
                break;
            case CardTypes.DEFESA:
                GetComponent<Image>().color = Color.green;
                break;
            case CardTypes.ITEM:
                GetComponent<Image>().color = Color.yellow;
                break;
        }
    }

    public void UseCard()
    {
        if (GameManager.instance.currentState == GameStates.ENDMATCH)
        {
            //reset combat
            StartCoroutine(CardManager.instance.ResetCombatWithNewDeck());
            //add new card to deck
            CardManager.instance.currentAvailableCards.Add(card);
        }
        if (GameManager.instance.currentState == GameStates.COMBAT)
        {
            if (CardManager.instance.CanUseCard((this)))
            {
	            Instantiate(_particle, transform.position, transform.rotation, transform.parent.parent);

                CardManager.instance.currentStamina -= cardStamina;
                
                //after the stamina has been removed, do cool card stuff
                switch (cardType)
                {
                    case CardTypes.ATAQUE:
                        CombatManager.instance.Attack(strength, this);
                        _audioSource.PlayOneShot(_attackSFX);
                        break;
                    case CardTypes.DEFESA:
                        CombatManager.instance.AddDefense(defense, this);
                        _audioSource.PlayOneShot(_defenseSFX);
                        if (cardDraw > 0)
                        {
                            StartCoroutine(CardManager.instance.DrawCards(this));
                        }
                        if (cure > 0)
                        {
                            CombatManager.instance.Heal(cure, this);
                        }
                        break;
                    case CardTypes.ITEM:
	                    _audioSource.PlayOneShot(_itemSFX);
                        if (cure > 0)
                        {
                            CombatManager.instance.Heal(cure, this);
                        }
                        break;
                    case CardTypes.BOOST:
                        break;
                }

                UIManager.instance.UpdateDisplay();
            }
        }
    }
}

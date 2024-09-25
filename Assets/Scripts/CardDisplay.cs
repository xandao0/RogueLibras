using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CardDisplay : MonoBehaviour
{
    public CardDataSo card;
    
    [Header("Card Info")]
    public TMP_Text cardNameText;
    public TMP_Text cardDescriptionText;
    //public Image artworkImage;
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
    
    // Start is called before the first frame update
    private void Start()
    {
        CollectInfoFromCardSo();
    }

    private void CollectInfoFromCardSo()
    {
        if (card == null)
        {
            Destroy(this.gameObject);
            return;
        }

        cardName = card.cardName;
        cardDesc = card.cardDescription;
        cardType = card.type;
        cardStamina = card.cardStamina;
        strength = card.strength;
        defense = card.defense;
        cardDraw = card.cardDrawAmount;

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
    }

    private string ProcessDescription()
    {
        string[] temp = cardDesc.Split(' ');
        string newCardDesc = "";
        cardDesc = "";
        
        switch (card.type)
        {
            case CardTypes.ATTACK:
                
                for (int i = 0; i < temp.Length; i++)
                {
                    if (temp[i].ToUpper() == "X")
                    {
                        temp[i] = strength.ToString();
                    }
                    
                    newCardDesc += temp[i];
                    if (i < temp.Length - 1)
                        newCardDesc += " ";
                }

                break;            
            case CardTypes.DEFENSE:
                for (int i = 0; i < temp.Length; i++)
                {
                    if (temp[i].ToUpper() == "X")
                    {
                        temp[i] = defense.ToString();
                    }
                    
                    newCardDesc += temp[i];
                    if (i < temp.Length - 1)
                        newCardDesc += " ";
                }

                break;            
            case CardTypes.ITEM:
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

    public void UseCard()
    {
        if (GameManager.instance.currentState == GameStates.ENDMATCH)
        {
            //reset combat
            //add new card to deck
        }
        else if (GameManager.instance.currentState == GameStates.COMBAT)
        {
            if (CardManager.instance.CanUseCard((this)))
            {
                CardManager.instance.currentStamina -= cardStamina;
                
                //after the stamina has been removed, do cool card stuff
                switch (cardType)
                {
                    case CardTypes.ATTACK:
                        CombatManager.instance.Attack(strength, this);
                        break;
                    case CardTypes.DEFENSE:
                        CombatManager.instance.AddDefense(defense, this);

                        if (cardDraw > 0)
                        {
                            StartCoroutine(CardManager.instance.DrawCards(this));
                        }
                        break;
                    case CardTypes.ITEM:
                        if (cardDraw > 0)
                        {
                            StartCoroutine(CardManager.instance.DrawCards(this));
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

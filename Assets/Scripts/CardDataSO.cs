using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Card", menuName = "New Card")]
public class CardDataSo : ScriptableObject
{
    public string cardName;
    public CardTypes type;
    public int cardStamina;//card's cost
    
    [Multiline]//adds more space to the string field
    public string cardDescription;//what the card does
    
    //public Sprite cardArt;
    public int strength;//card's damage
    public int defense;//card's block
    public int cardDrawAmount;//card's carddraw
    
}

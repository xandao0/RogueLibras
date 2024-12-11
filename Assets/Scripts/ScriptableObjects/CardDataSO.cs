using UnityEngine;
using UnityEngine.Video;

[CreateAssetMenu(fileName = "Card", menuName = "New Card")]
public class CardDataSo : ScriptableObject
{
    public string cardName;
    public CardTypes type;
    public int cardStamina;//card's cost
    
    [Multiline]//adds more space to the string field
    public string cardDescription;//what the card does

    public VideoClip cardVideo;
    
    //public Sprite cardArt;
    public int strength;//card's damage
    public int defense;//card's block
    public int cardDrawAmount;//card's carddraw
    public int cure;
    public CardEffects[] cardEffects;
    
    [System.Serializable]
    public struct CardEffects
    {
        public StatusEffects effect;//which effect gets triggered
        public int length;//how many turns does this persist for
    }
    
}

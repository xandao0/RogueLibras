using UnityEngine;
using UnityEngine.Video;

[CreateAssetMenu(fileName = "Card", menuName = "New Card")]
public class CardDataSo : ScriptableObject
{
    public string cardName;
    public CardTypes type;

    // which SignWriting category signs appear on the card (comum = two signs)
    public CardCategories cardCategory = CardCategories.COMUM;
    public int cardStamina; // card's cost

    [Multiline] // adds more space to the string field
    public string cardDescription; // what the card does

    [Header("Art")]
    public Sprite cardBackground;

    [Tooltip("3D model animation played on top of the background.")]
    public VideoClip cardVideo;

    // public Sprite cardArt;
    public int strength;        // card's damage
    public int defense;         // card's block
    public int cardDrawAmount;  // card's carddraw
    public int cure;
    public CardEffects[] cardEffects;

    [System.Serializable]
    public struct CardEffects
    {
        public StatusEffects effect; // which effect gets triggered
        public int length;           // how many turns does this persist for
    }
}
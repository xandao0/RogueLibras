using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Trinket_", menuName = "New Trinket")]
public class TrinketDataSO : ScriptableObject
{
    public string trinketName;

    public Sprite trinketSprite;
    [TextArea(5,10)]
    public string trinketDesc;
    public TrinketEffects[] allTrinketEffects;
    
    [System.Serializable]
    public struct TrinketEffects
    {
        public StatusEffects effect;
        public float amount;
    }
}

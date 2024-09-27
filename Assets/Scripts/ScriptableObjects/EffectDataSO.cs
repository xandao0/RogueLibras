using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Effect_", menuName = "New Effect")]
public class EffectDataS : ScriptableObject
{
    public StatusEffects effectType;
    public string effectName;
    public float effectStrength;
    public Sprite effectSprite;
    public string effectDesc;
}

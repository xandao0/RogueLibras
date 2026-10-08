using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Status : MonoBehaviour
{
    public EffectDataSO effectData;

    public string effectName;
    public Sprite effectSprite;
    [TextArea(5, 10)] 
    public string effectDesc;
    public float effectStrength;
    public StatusEffects effectType;

    public int turnsLeft;

    public TMP_Text statusText;
    public TMP_Text tittleText;
    public TMP_Text descText;

    private void Start()
    {
        LoadTrinketInfoFromData();
    }

    private void LoadTrinketInfoFromData()
    {
        effectName = Loc.EffectName(effectData);
        effectSprite = effectData.effectSprite;
        effectDesc = Loc.EffectDescription(effectData);
        effectType = effectData.effectType;
        effectStrength = effectData.effectStrength;

        GetComponent<Image>().sprite = effectSprite;

        statusText.text = turnsLeft.ToString();

        tittleText.text = effectName;   // Camel case, matching the rest of the UI
        descText.text = effectDesc;
    }
}

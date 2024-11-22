using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class Enemy : MonoBehaviour
{
    public EnemyDataSO eData;

    [Header("Enemy Info")] 
    public string eName;
    public EnemyTypes eType;
    public int maxHP;
    public int strength;
    public int blockedDemage;

    public Slider healthSlider;
    public Image healthSliderFill;
    public Color blockedColor;

    public GameObject currentBlockedDisplay;

    public TMP_Text healthText;
    public TMP_Text enemyName;

    [Header("Enemy Intents Display")] 
    public Image intentImage;
    public TMP_Text intentAmtText;
    public TMP_Text blockedAmtDisplay;

    public Sprite sprite_IntentAttack;
    public Sprite sprite_IntentDefense;
    public Sprite sprite_IntentBuff;
    public Sprite sprite_IntentDisable;

    public List<EnemyDataSO.EnemyIntents> thisTurnIntent = new List<EnemyDataSO.EnemyIntents>();
    public int thisTurnIntentStrength;

    [SerializeField] 
    private int currentHP;
    public int CurrentHP
    {
        get { return currentHP; }
        set { currentHP = value; HandleHealth();}
    }

    public List<StatusEffects> currentStatusEffects = new List<StatusEffects>();
    public List<int> currentStatusEffectLengths = new List<int>();
    public Transform enemyStatusEffectsContainer;

    private void Start()
    {
        EffectsManager.instance.enemyStatusContainer = enemyStatusEffectsContainer;
        
        CollectInfoFromData();
    }

    private void CollectInfoFromData()
    {
        if (eData == null)
        {
            Destroy(this.gameObject);
            return;
        }

        eName = eData.enemyName;
        eType = eData.enemyType;
        maxHP = eData.maxHP;
        strength = eData.strength;

        healthSlider.maxValue = maxHP;
        enemyName.text = eName.ToUpper();
        CurrentHP = maxHP;
    }

    private void HandleHealth()
    {
        if (currentHP <= 0)
        {
            currentHP = 0;
            //show end match screen
            UIManager.instance.endMatchGO.SetActive(true);
            Destroy(this.gameObject);
        }

        if (currentHP > maxHP)
        {
            currentHP = maxHP;
        }

        healthSlider.value = currentHP;
        healthText.text = string.Format("{0}/{1}", currentHP, maxHP);

        if (blockedDemage <= 0)
        {
            blockedDemage = 0;
            healthSliderFill.color = Color.red;
            currentBlockedDisplay.SetActive(false);
        }
    }

    public void TakeDamage(int d)
    {
        int dmg = d;
        
        //status effects
        if (currentStatusEffects.Contains(StatusEffects.VULNERABLE))
        {
            dmg = Mathf.RoundToInt(dmg * EffectsManager.instance.GetStatusEffect(StatusEffects.VULNERABLE)
                .effectStrength);
        }

        if (blockedDemage >= dmg) //if the enemies block is higher than your damage you are inflicting
            blockedDemage -= dmg;
        else
        {
            dmg -= blockedDemage;

            CurrentHP -= dmg;
        }

        blockedAmtDisplay.text = blockedDemage.ToString();
    }

    public void HealHealth(int d)
    {
        CurrentHP += d;
    }

    public void AddDefense(int d)
    {
        blockedDemage += d;

        if (blockedDemage > 0)
        {
            currentBlockedDisplay.SetActive(true);
            healthSliderFill.color = blockedColor;
            blockedAmtDisplay.text = blockedDemage.ToString();
        }
        
        HandleHealth();
    }

    //when player draws new cards
    public void OnNewTurn()
    {
        thisTurnIntent.Clear();
        
        //enemymanager chooose next intents
        EnemyManager.instance.ChooseIntentsForNextTurn(this);
    }

    public void AddEffect(StatusEffects effect, int length)
    {
        if (!currentStatusEffects.Contains(effect))
        {
            EffectsManager.instance.AddEffect(CurrentTurn.ENEMYTURN, effect);
            currentStatusEffectLengths.Add(length);
            EffectsManager.instance.CreateStatus(effect, true);
        }
        else
        {
            for (int i = 0; i < currentStatusEffects.Count; i++)
            {
                if (currentStatusEffects[i] == effect)
                {
                    currentStatusEffectLengths[i] += length;
                }
            }
        }

        UpdateUIStatusContainer();
    }

    public void UpdateUIStatusContainer()
    {
        if (currentStatusEffects.Count == 0)
        {
            return;
        }

        for (int i = currentStatusEffects.Count - 1; i >= 0; i--)
        {
            EffectsManager.instance.enemyStatusContainer.GetChild(i).GetComponent<Status>().statusText.text =
                currentStatusEffectLengths[i].ToString();
        }
    }

    public void ReduceStatusEffectsOnNewTurn()
    {
        for (int i = 0; i < currentStatusEffects.Count; i++)
        {
            currentStatusEffectLengths[i]--;
        }

        for (int i = currentStatusEffectLengths.Count - 1; i >= 0 ; i--)
        {
            if (currentStatusEffectLengths[i] <= 0)
            {
                EffectsManager.instance.RemoveStatus(EffectsManager.instance.enemyStatusContainer.GetChild(i).gameObject);
                
                currentStatusEffectLengths.RemoveAt(i);
                currentStatusEffects.RemoveAt(i);
            }
        }
        
        UpdateUIStatusContainer();
    }
}

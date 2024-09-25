using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
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

    [Header("Enemy Intents Display")] public Image intentImage;
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

    private void Start()
    {
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
}

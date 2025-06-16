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

    public SpriteRenderer enemyArt;

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
        set { currentHP = value; HandleHealth(); }
    }

    public List<StatusEffects> currentStatusEffects = new List<StatusEffects>();
    public List<int> currentStatusEffectLengths = new List<int>();
    public Transform enemyStatusEffectsContainer;

    private void Start()
    {
        CollectInfoFromData();
    }

    private void CollectInfoFromData()
    {
        if (eData == null)
        {
            Destroy(gameObject);
            return;
        }

        eName = eData.enemyName;
        eType = eData.enemyType;
        maxHP = eData.maxHP + EnemyManager.instance.defeatedEnemiesCount * 2; // AUMENTO CADA VEZ QUE UM É DERROTADO
        strength = eData.strength;

        healthSlider.maxValue = maxHP;
        enemyName.text = eName.ToUpper();
        CurrentHP = maxHP;

        enemyArt.sprite = eData.artwork;
    }

    private void HandleHealth()
    {
        if (currentHP <= 0)
        {
            EnemyManager.instance.OnEnemyDefeated();  // Atualiza contador de derrotas e bônus no Manager
            UIManager.instance.endMatchGO.SetActive(true);
            Destroy(gameObject);
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

        if (blockedDemage >= dmg)
            blockedDemage -= dmg;
        else
        {
            dmg -= blockedDemage;
            blockedDemage = 0;
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

    public void OnNewTurn()
    {
        thisTurnIntent.Clear();
        EnemyManager.instance.ChooseIntentsForNextTurn(this);
    }
}

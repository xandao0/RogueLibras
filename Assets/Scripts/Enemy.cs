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

    // Drives the lunge / flinch / guard beats. Optional: combat still works
    // without it, it just plays silently.
    public EnemyReaction reaction;

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

    // Set by EnemyManager when this is the enemy a loaded save was fighting, so
    // it comes back as wounded as it was. Below 1 means start at full health.
    [HideInInspector] public int restoreHP = -1;

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
        if (reaction == null) reaction = GetComponent<EnemyReaction>();
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
        enemyName.text = eName;   // Camel case as authored; no shouting
        CurrentHP = restoreHP > 0 ? Mathf.Min(restoreHP, maxHP) : maxHP;

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
            // The fill keeps whatever colour the prefab sets. Recolouring the
            // health bar while blocking read as the bar itself changing state;
            // the shield icon and its number already say the enemy is guarded.
            currentBlockedDisplay.SetActive(false);
            blockedDemage = 0;
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
        if (reaction != null) reaction.PlayHurt();
        HandleHealth();
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
            blockedAmtDisplay.text = blockedDemage.ToString();
        }

        if (reaction != null) reaction.PlayDefend();
        HandleHealth();
    }

    public void OnNewTurn()
    {
        thisTurnIntent.Clear();
        EnemyManager.instance.ChooseIntentsForNextTurn(this);
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Enemy : MonoBehaviour, ISignWordSource
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

    [Header("SignWriting")]
    [Tooltip("Size of the signs that replace the name and numbers, relative to the text.")]
    [Range(0.6f, 3f)] public float signScale = 1.25f;

    // What the intent badge shows; -1 when it shows no number.
    private int _intentAmount = -1;

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
        // Big HUD text: signs without the card outline, which smears them here.
        foreach (var t in new[] { enemyName, healthText, blockedAmtDisplay, intentAmtText }) SignText.UsePlainSigns(t);
        CollectInfoFromData();
    }

    private void CollectInfoFromData()
    {
        if (eData == null)
        {
            Destroy(gameObject);
            return;
        }

        eName = Loc.EnemyName(eData);   // in the active language; see Assets/Locales
        eType = eData.enemyType;
        maxHP = eData.maxHP + EnemyManager.instance.defeatedEnemiesCount * 2; // AUMENTO CADA VEZ QUE UM É DERROTADO
        strength = eData.strength;

        healthSlider.maxValue = maxHP;
        enemyName.text = SignText.Staged(eName, eName, signScale);   // Camel case as authored; no shouting
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
        healthText.text = HealthText();

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

        blockedAmtDisplay.text = SignText.Number(blockedDemage, signScale);
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
            blockedAmtDisplay.text = SignText.Number(blockedDemage, signScale);
        }

        if (reaction != null) reaction.PlayDefend();
        HandleHealth();
    }

    // ---- SignWriting ----------------------------------------------------------
    // The name, health, block and intent numbers turn into signs one at a time
    // along with the words on the cards (see SignProgression).

    private string HealthText()
    {
        return SignText.Number(currentHP, signScale) + "/" + SignText.Number(maxHP, signScale);
    }

    /// The number on the intent badge, or none (-1).
    public void ShowIntentAmount(int amount)
    {
        _intentAmount = amount;
        if (intentAmtText != null) intentAmtText.text = amount >= 0 ? SignText.Number(amount, signScale) : "";
    }

    public void CollectSignWords(List<string> into)
    {
        SignText.AddWord(into, eName);
        SignText.NumberWords(currentHP, into);
        SignText.NumberWords(maxHP, into);
        if (blockedDemage > 0) SignText.NumberWords(blockedDemage, into);
        if (_intentAmount >= 0) SignText.NumberWords(_intentAmount, into);
    }

    public void RefreshSignText()
    {
        if (enemyName != null) enemyName.text = SignText.Staged(eName, eName, signScale);
        if (healthText != null) healthText.text = HealthText();
        if (blockedAmtDisplay != null) blockedAmtDisplay.text = SignText.Number(blockedDemage, signScale);
        ShowIntentAmount(_intentAmount);
    }

    public void OnNewTurn()
    {
        thisTurnIntent.Clear();
        EnemyManager.instance.ChooseIntentsForNextTurn(this);
    }
}

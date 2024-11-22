using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class EffectsManager : MonoBehaviour
{
    public static EffectsManager instance;

    public EffectDataSO vulnerable;
    public EffectDataSO weak;
    public EffectDataSO frail;
    public EffectDataSO might;

    public Dictionary<StatusEffects, EffectDataSO> statusDict = new Dictionary<StatusEffects, EffectDataSO>();

    [Header("Display")]
    public GameObject playerStatusPrefab;
    public Transform playerStatusContainer;
    public Transform enemyStatusContainer;
    public GameObject enemyStatusPrefab;

    private void Awake()
    {
        if (instance && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private void Start()
    {
        statusDict.Clear();
        
        statusDict.Add(StatusEffects.VULNERABLE, vulnerable);
        statusDict.Add(StatusEffects.WEAK, weak);
        statusDict.Add(StatusEffects.FRAIL, frail);
        statusDict.Add(StatusEffects.MIGHT, might);
    }

    public EffectDataSO GetStatusEffect(StatusEffects s)
    {
        return statusDict[s];
    }

    public void CreateStatus(StatusEffects effect, bool isEnemy)
    {
        if (isEnemy)
        {
            GameObject g = Instantiate(enemyStatusPrefab, enemyStatusContainer);
            g.GetComponent<Status>().effectData = GetStatusEffect(effect);
            g.GetComponent<Status>().turnsLeft =
                CombatManager.instance.currentEnemy.currentStatusEffectLengths
                [CombatManager.instance.currentEnemy.currentStatusEffectLengths.Count - 1];
        }
        else
        {
            GameObject g = Instantiate(playerStatusPrefab, playerStatusContainer);
            g.GetComponent<Status>().effectData = GetStatusEffect(effect);
            g.GetComponent<Status>().turnsLeft = CombatManager.instance.currentStatusEffectsLengths
                [CombatManager.instance.currentStatusEffectsLengths.Count - 1];
        }
    }

    public void RemoveStatus(GameObject g)
    {
        Destroy(g);
    }

    public void UpdateUIStatusContainer()
    {
        if (CombatManager.instance.currentStatusEffects.Count == 0)
        {
            return;
        }

        for (int i = CombatManager.instance.currentStatusEffects.Count - 1; i >= 0; i--)
        {
            playerStatusContainer.GetChild(i).GetComponent<Status>().statusText.text =
                CombatManager.instance.currentStatusEffectsLengths[i].ToString();
        }
    }

    public void AddEffect(CurrentTurn effected, StatusEffects effect)
    {
        switch (effected)
        {
            case CurrentTurn.PLAYERTURN:
                CombatManager.instance.currentStatusEffects.Add(effect);
                break;
            case CurrentTurn.ENEMYTURN:
                CombatManager.instance.currentEnemy.currentStatusEffects.Add(effect);
                break;
        }
    }
}

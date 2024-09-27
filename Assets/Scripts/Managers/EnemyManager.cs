using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class EnemyManager : MonoBehaviour
{
   public static EnemyManager instance;

   public Transform spawnSpot;

   public GameObject enemyPrefab;

   [Header("ALL ENEMY DATA HERE")] 
   public List<int> enemyIndex = new List<int>();

   public List<EnemyDataSO> enemyIndexedData = new List<EnemyDataSO>();
   public Dictionary<int, EnemyDataSO> enemyDictionary = new Dictionary<int, EnemyDataSO>(); 
   
   private void Awake()
   {
      if (instance != null && instance != this)
      {
         Destroy(this.gameObject);
         return;
      }

      instance = this;
      
      for (int i = 0; i < enemyIndexedData.Count; i++)
      {
         enemyIndex.Add(i);
      }

      for (int i = 0; i < enemyIndex.Count; i++)
      {
         enemyDictionary.Add(i, enemyIndexedData[i]);
      }
   }

   private void Start()
   {
      
   }

   public void SpawnEnemy()
   {
      int r = Random.Range(0, enemyDictionary.Count);
      GameObject g = Instantiate(enemyPrefab, spawnSpot);
      Enemy e = g.GetComponent<Enemy>();
      
      e.eData = enemyDictionary[r];
      CombatManager.instance.currentEnemy = e; 
   }

   public void ChooseIntentsForNextTurn(Enemy e)
   {
      int r = Random.Range(0, e.eData.allIntents.Length);
      
      e.thisTurnIntent.Clear();

      EnemyDataSO.EnemyIntents intent = e.eData.allIntents[r];

      for (int i = 0; i < intent.intent.Length; i++)
      {
         e.thisTurnIntent.Add(intent);
      }

      e.thisTurnIntentStrength = intent.amount;

      switch (e.thisTurnIntent[0].intent[0])
      {
         case EnemyIntentsType.ATTACK:
            int dmg = e.thisTurnIntentStrength;

            e.intentImage.sprite = e.sprite_IntentAttack;
            e.intentAmtText.text = dmg.ToString();
            break;
         case EnemyIntentsType.DEFEND:
            e.intentImage.sprite = e.sprite_IntentDefense;
            e.intentAmtText.text = "";
            break;
         case EnemyIntentsType.BUFF:
            e.intentImage.sprite = e.sprite_IntentBuff;
            e.intentAmtText.text = "";
            break;
         case EnemyIntentsType.DISABLE:
            e.intentImage.sprite = e.sprite_IntentDisable;
            e.intentAmtText.text = "";
            break;
      }
   }

   public IEnumerator TakeEnemyTurn(Enemy e)
   {
      yield return new WaitForSeconds(0.5f);
      //do intent and VFX
      for (int i = 0; i < e.thisTurnIntent.Count; i++)
      {
         switch (e.thisTurnIntent[i].intent[i])
         {
            case EnemyIntentsType.ATTACK:
               CombatManager.instance.TakeDamage((e.thisTurnIntentStrength));
               break;
            case EnemyIntentsType.DEFEND:
               e.AddDefense(e.thisTurnIntentStrength);
               break;
            case EnemyIntentsType.BUFF:
               //To do: add status effect to enemy
               break;
            case EnemyIntentsType.DISABLE:
               //To do: add status effect to player
               break;
         }
      }

      yield return new WaitForSeconds(1.5f);
      EndEnemyTurn();
   }

   public void EndEnemyTurn()
   {
      //To do: reduce player status effects at end of turn

      CardManager.instance.StartNewTurn();
   }
}

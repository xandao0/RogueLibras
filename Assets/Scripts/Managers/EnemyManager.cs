using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

public class EnemyManager : MonoBehaviour
{
   public static EnemyManager instance { get; private set; }

   [Header("Setup")]
   public Transform spawnSpot;
   public GameObject enemyPrefab;

   [Header("Enemy Data")] 
   [Tooltip("Insira os Scriptable Objects dos inimigos aqui.")]
   public List<EnemyDataSO> enemyIndexedData = new List<EnemyDataSO>();
   
   [Header("Settings & Stats")]
   [SerializeField] private int bonusHealth = 0; // Controla o bônus de vida acumulado
   public int defeatedEnemiesCount { get; private set; } = 0;
   
   private void Awake()
   {
      if (instance != null && instance != this)
      {
         Destroy(gameObject);
         return;
      }
      instance = this;
   }

   public void SpawnEnemy()
   {
      if (enemyIndexedData.Count == 0)
      {
         Debug.LogError("Nenhum inimigo cadastrado na lista 'enemyIndexedData'!");
         return;
      }

      int randomIndex = Random.Range(0, enemyIndexedData.Count);
      EnemyDataSO selectedData = enemyIndexedData[randomIndex];

      GameObject spawnedObj = Instantiate(enemyPrefab, spawnSpot);
      Enemy enemyComponent = spawnedObj.GetComponent<Enemy>();
      
      enemyComponent.eData = selectedData;
      
      //Calcula e aplica a vida máxima com o bônus
      int newMaxHP = selectedData.maxHP + bonusHealth;
      enemyComponent.maxHP = newMaxHP;  
      enemyComponent.CurrentHP = newMaxHP;  
      
      CombatManager.instance.currentEnemy = enemyComponent; 
   }
   
   public void OnEnemyDefeated()
   {
      defeatedEnemiesCount++;
   }

   public void ChooseIntentsForNextTurn(Enemy enemy)
   {
      if (enemy.eData.allIntents == null || enemy.eData.allIntents.Length == 0) return;

      int randomIndex = Random.Range(0, enemy.eData.allIntents.Length);
      enemy.thisTurnIntent.Clear();

      EnemyDataSO.EnemyIntents intent = enemy.eData.allIntents[randomIndex];

      for (int i = 0; i < intent.intent.Length; i++)
      {
         enemy.thisTurnIntent.Add(intent);
      }

      enemy.thisTurnIntentStrength = intent.amount;

      if (enemy.thisTurnIntent.Count > 0 && intent.intent.Length > 0)
      {
         EnemyIntentsType currentType = intent.intent[0];

         switch (currentType)
         {
            case EnemyIntentsType.ATTACK:
               int dmg = enemy.thisTurnIntentStrength;
               enemy.intentImage.sprite = enemy.sprite_IntentAttack;
               enemy.intentAmtText.text = dmg.ToString(); // Mostra o valor do ataque
               break;

            case EnemyIntentsType.DEFEND:
               enemy.intentImage.sprite = enemy.sprite_IntentDefense;
               enemy.intentAmtText.text = ""; 
               break;
         }
      }
   }

   public IEnumerator TakeEnemyTurn(Enemy enemy)
   {
      yield return new WaitForSeconds(0.5f);

      for (int i = 0; i < enemy.thisTurnIntent.Count; i++)
      {
         if (i >= enemy.thisTurnIntent[i].intent.Length) continue;

         EnemyIntentsType currentType = enemy.thisTurnIntent[i].intent[i];

         switch (currentType)
         {
            case EnemyIntentsType.ATTACK:
               CombatManager.instance.TakeDamage(enemy.thisTurnIntentStrength);
               break;
            case EnemyIntentsType.DEFEND:
               enemy.AddDefense(enemy.thisTurnIntentStrength);
               break;
               
            //case EnemyIntentsType.BUFF:
            //case EnemyIntentsType.DISABLE:
         }
      }

      yield return new WaitForSeconds(1.5f);
      EndEnemyTurn();
   }
   
   public int GetRandomEnemyIndex()
   {
      return Random.Range(0, enemyIndexedData.Count);
   }

   private void EndEnemyTurn()
   {
      //CombatManager.instance.ReduceAllEffectsOnPlayer();
      CardManager.instance.StartNewTurn();
   }
}
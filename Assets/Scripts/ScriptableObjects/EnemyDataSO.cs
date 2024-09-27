using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Enemy_", menuName = "New Enemy")]
public class EnemyDataSO : ScriptableObject
{
    public EnemyTypes enemyType;
    public string enemyName;
    public int maxHP;
    public int strength;
    public EnemyIntents[] allIntents;
    
    [System.Serializable]
    public struct EnemyIntents
    {
        public EnemyIntentsType[] intent;
        public int amount;
    }
}

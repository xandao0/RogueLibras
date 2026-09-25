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

    [Tooltip("The resting pose, shown whenever the enemy is not acting.")]
    public Sprite artwork;

    // Optional frame sequences for the enemy's turn. Each plays in order and the
    // last frame is held; leave one empty and that beat falls back to moving the
    // resting pose around (see EnemyReaction). Frames of different canvas sizes
    // line up because each texture's pixels-per-unit and pivot are set to match
    // the resting pose.
    [Header("Turn Animation (optional)")]
    [Tooltip("Getting ready to attack - the 'waiting to act' pose.")]
    public Sprite[] attackWindupFrames;
    [Tooltip("The attack itself. Damage lands on the first frame.")]
    public Sprite[] attackFrames;
    [Tooltip("Getting ready to defend.")]
    public Sprite[] defendWindupFrames;
    [Tooltip("The guard itself. Block is gained on the first frame.")]
    public Sprite[] defendFrames;

    [System.Serializable]
    public struct EnemyIntents
    {
        public EnemyIntentsType[] intent;
        public int amount;
        //public EnemyIntentEffects[] intentEffects;
    }
    /*
    [System.Serializable]
    public struct EnemyIntentEffects
    {
        public StatusEffects effect;
        public int effectTurns;
    }*/
}

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CombatManager : MonoBehaviour
{
    public static CombatManager instance;

    public Enemy currentEnemy;
    
    public int startingHealth = 50;
    public int currentBlock = 0;
    public int currentMight = 0;

    public int maxHealth;
    [SerializeField] private int currentHealth;
    public int CurrentHealth
    {
        get
        {
            return currentHealth;
        }
        set 
        { 
            currentHealth = value;
            UpdateHealthDisplay();
        }
    }
    
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
            return;
        }

        instance = this;
    }

    private void Start()
    {
        maxHealth = startingHealth;
        CurrentHealth = maxHealth;
        
        UIManager.instance.UpdateDisplay();
    }

    private void UpdateHealthDisplay()
    {
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        
        UIManager.instance.UpdateDisplay();
    }

    public void Attack(int d, CardDisplay card)
    {
        //current enemy to take damage
        if (card != null)
        {
            CardManager.instance.DiscardCard(card);
        }

        CardManager.instance.UpdateDisplay();
    }

    public void AddDefense(int d, CardDisplay card)
    {
        currentBlock += d;
        if(card!=null)
            CardManager.instance.DiscardCard(card);
        
        CardManager.instance.UpdateDisplay();
    }

    public void TakeDamage(int d)
    {
        int dmg = d;

        if (currentBlock >= dmg)
            currentBlock -= dmg;
        else
        {
            dmg -= currentBlock;
            currentBlock = 0;
            CurrentHealth -= dmg;
        }
    }

    public void Heal(int d)
    {
        CurrentHealth += d;
    }
}

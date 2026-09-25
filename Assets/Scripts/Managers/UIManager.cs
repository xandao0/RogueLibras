using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UIManager : MonoBehaviour
{
   public static UIManager instance;

   public TMP_Text healthText;
   public TMP_Text blockedText;
   public TMP_Text staminaText;

   [Header("Block bar (replaces the Bloqueio text)")]
   public BlockedBar blockedBar;

   [Header("3D lifebar (replaces the Vida / Energia text)")]
   [Tooltip("LifebarController on the 3D bar. The text fields above may be left " +
            "empty once this is hooked up.")]
   public LifebarController lifebar;

   public GameObject endMatchGO;

   public GameObject endGameGO;

   private void Awake()
   {
      if (instance != null && instance != this)
      {
         Destroy(gameObject);
         return;
      }

      instance = this;
   }

   public void UpdateDisplay()
   {
      if (healthText != null)
         healthText.text = string.Format("{0}<color=#FFFFFF>/</color>{1}", CombatManager.instance.CurrentHealth,
            CombatManager.instance.maxHealth);

      if (blockedText != null)
         blockedText.text = string.Format("Bloqueio: {0}", CombatManager.instance.currentBlock);

      if (blockedBar != null)
         blockedBar.SetBlock(CombatManager.instance.currentBlock);

      if (staminaText != null)
         staminaText.text = string.Format("Energia: <color=#00FF00>{0}</color> / <color=#00FF00>{1}</color>",
            CardManager.instance.currentStamina, CardManager.instance.staminaAtStart);

      if (lifebar != null)
      {
         lifebar.SetHealth(CombatManager.instance.CurrentHealth, CombatManager.instance.maxHealth);
         lifebar.SetEnergy(CardManager.instance.currentStamina, CardManager.instance.staminaAtStart);
      }
   }
}

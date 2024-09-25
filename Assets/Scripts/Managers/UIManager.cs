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

   private void Awake()
   {
      if (instance != null && instance != this)
      {
         Destroy(this.gameObject);
         return;
      }

      instance = this;
   }

   public void UpdateDisplay()
   {
      healthText.text = string.Format("{0}<color=#FFFFFF>/</color>{1}", CombatManager.instance.CurrentHealth,
         CombatManager.instance.maxHealth);
      blockedText.text = string.Format("BLOCKED: {0}", CombatManager.instance.currentBlock);
      staminaText.text = string.Format("Stamina: <color=#00FF00>{0}</color> / <color=#00FF00>{1}</color>",
         CardManager.instance.currentStamina, CardManager.instance.staminaAtStart);
   }
}

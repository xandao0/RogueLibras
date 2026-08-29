using UnityEngine;

public class StaminaManager : MonoBehaviour
{
    [Header("Configurações de Estamina")]
    public int staminaAtStart = 3;
    public int currentStamina = 3;

    public void ResetStamina()
    {
        currentStamina = staminaAtStart;
    }

    public bool HasEnoughStamina(int cost)
    {
        return currentStamina > 0 && cost <= currentStamina;
    }
}
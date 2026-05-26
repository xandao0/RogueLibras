using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerManager : MonoBehaviour
{
    public int startingHealth = 50;
    public int maxHealth;

    public int currentHealth;
    public int currentBlock;
    public int currentMight;

    void Start()
    {
        maxHealth = startingHealth;
        currentHealth = maxHealth;
    }
//dano
    public void TakeDamage(int damage)
    {
        int dmg = damage;

        if (currentBlock >= dmg)
        {
            currentBlock -= dmg;
        }
        else
        {
            dmg -= currentBlock;
            currentBlock = 0;
            currentHealth -= dmg;
        }

        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        UIManager.instance.UpdateDisplay();

        if (currentHealth <= 0)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex - 1);
        }
    }
//cura
    public void Heal(int heal)
    {
        currentHealth += heal;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);

        UIManager.instance.UpdateDisplay();
    }
//defesa
    public void AddDefense(int defense)
    {
        currentBlock += defense;

        UIManager.instance.UpdateDisplay();
    }
}
using System;
using System.Collections.Generic;

// One saved run. Cards are stored by asset name rather than by reference so
// the file stays readable and survives asset reordering.
[Serializable]
public class SaveData
{
    // 2 added the enemy and the sign progression. Older files load without
    // them: a random enemy, and the signs stay however they currently are.
    public const int CurrentVersion = 2;
    public int version;

    public int slot;
    public string savedAtUtc;      // round-trip ISO timestamp
    public int sceneBuildIndex;    // where the player was

    public int currentHealth;
    public int maxHealth;
    public int currentBlock;

    public int defeatedEnemies;

    public List<string> deckCardNames = new List<string>();

    // The enemy being fought, by asset name, and how hurt it was.
    public string enemyDataName;
    public int enemyHP;

    // Which words had already turned into SignWriting, and how far along the
    // one-word-per-turn schedule the run was.
    public List<string> learnedSigns = new List<string>();
    public int signTurn;
    public int nextSignTurn;

    public bool HasSignProgress { get { return version >= 2; } }

    public DateTime SavedAt
    {
        get
        {
            DateTime parsed;
            return DateTime.TryParse(savedAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out parsed)
                ? parsed
                : DateTime.MinValue;
        }
    }

    public string Describe()
    {
        DateTime at = SavedAt;
        string when = at != DateTime.MinValue ? at.ToLocalTime().ToString("dd/MM/yyyy HH:mm") : savedAtUtc;

        string text = string.Format("{0}  •  {1} HP  •  {2} inimigos  •  {3} cartas",
            when, currentHealth, defeatedEnemies, deckCardNames != null ? deckCardNames.Count : 0);

        if (HasSignProgress)
            text += string.Format("  •  {0} sinais", learnedSigns != null ? learnedSigns.Count : 0);

        return text;
    }
}

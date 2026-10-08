using System;
using System.Collections.Generic;

// One saved run. Cards are stored by asset name rather than by reference so
// the file stays readable and survives asset reordering.
[Serializable]
public class SaveData
{
    // 2 added the enemy and the sign progression. Older files load without
    // them: a random enemy, and the signs stay however they currently are.
    // 3 added the direction; anything older was Portuguese to Libras.
    public const int CurrentVersion = 3;
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

    // SignProgression.Direction, as an int so JsonUtility reads old files as 0.
    public int signDirection;

    public SignProgression.Direction Direction
    {
        get { return version >= 3 ? (SignProgression.Direction)signDirection : SignProgression.Direction.PortugueseToLibras; }
    }

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

    /// One line for a save slot. withDirection: false where the list is
    /// already grouped by direction (the start menu).
    public string Describe(bool withDirection = true)
    {
        DateTime at = SavedAt;
        string when = at != DateTime.MinValue ? at.ToLocalTime().ToString(Loc.T("save.date_format")) : savedAtUtc;

        string text = Loc.T("save.summary",
            when, currentHealth, defeatedEnemies, deckCardNames != null ? deckCardNames.Count : 0);

        if (HasSignProgress)
        {
            text += "  •  " + Loc.T("save.signs", learnedSigns != null ? learnedSigns.Count : 0);
            if (withDirection) text += "  •  " + SignProgression.Describe(Direction);
        }

        return text;
    }
}

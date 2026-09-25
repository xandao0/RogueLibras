using System.Collections.Generic;
using UnityEngine;

// Decides which words on the cards have turned into SignWriting yet.
//
// Everything starts as plain Portuguese. Nothing changes for the first few
// turns; after that, one single word graduates per turn, picked at random from
// whatever is actually on screen. Because the pick is per *word* and not per
// card, a card can end up half text and half sign - its name signed while its
// number is still a numeral - which is the whole point: the player meets one new
// sign at a time instead of a wall of them.
//
// Progress persists in PlayerPrefs, so signs learned in one run are still signs
// in the next. It is also written into every save (SaveSystem.Capture) and put
// back when that save is loaded, and it starts over when every save is deleted.
public static class SignProgression
{
    public enum Stage
    {
        Text = 0,   // still the Portuguese word
        Sign = 1    // graduated: the sign replaces it
    }

    [Tooltip("Turns played before the first word ever changes.")]
    public const int TurnsBeforeFirstSign = 3;

    // How many turns between graduations once they have started.
    public const int TurnsPerNewSign = 1;

    private const string LearnedKey = "sign.learned";
    private const string TurnKey = "sign.turn";
    private const string NextKey = "sign.next";

    private static HashSet<string> _learned;
    private static int _turn = -1;
    private static int _nextSignTurn = -1;

    private static HashSet<string> Learned
    {
        get
        {
            if (_learned == null)
            {
                _learned = new HashSet<string>();
                string raw = PlayerPrefs.GetString(LearnedKey, string.Empty);
                if (!string.IsNullOrEmpty(raw))
                    foreach (var w in raw.Split('|'))
                        if (!string.IsNullOrEmpty(w)) _learned.Add(w);
            }
            return _learned;
        }
    }

    /// Turns played so far this save.
    public static int Turn
    {
        get
        {
            if (_turn < 0) _turn = PlayerPrefs.GetInt(TurnKey, 0);
            return _turn;
        }
    }

    /// How many words have graduated so far.
    public static int LearnedCount { get { return Learned.Count; } }

    /// The turn on which the next word is due to graduate.
    public static int NextSignTurn
    {
        get
        {
            if (_nextSignTurn < 0) _nextSignTurn = PlayerPrefs.GetInt(NextKey, TurnsBeforeFirstSign + 1);
            return _nextSignTurn;
        }
    }

    /// Every graduated word, as normalized keys. A copy, for saving.
    public static List<string> LearnedWords()
    {
        var list = new List<string>(Learned);
        list.Sort(string.CompareOrdinal);
        return list;
    }

    /// Put the progression back to a saved point: exactly these words
    /// graduated, and the schedule where it was.
    public static void Restore(IList<string> learnedWords, int turn, int nextSignTurn)
    {
        Learned.Clear();
        if (learnedWords != null)
            foreach (var w in learnedWords)
                if (!string.IsNullOrEmpty(w)) Learned.Add(KeyFor(w));

        _turn = Mathf.Max(0, turn);
        _nextSignTurn = nextSignTurn > 0 ? nextSignTurn : Mathf.Max(TurnsBeforeFirstSign + 1, _turn + 1);
        PlayerPrefs.SetInt(TurnKey, _turn);
        Save();
    }

    private static string KeyFor(string word)
    {
        return SignWritingLibrary.Normalize(word);
    }

    public static bool IsLearned(string word)
    {
        if (string.IsNullOrEmpty(word)) return false;
        return Learned.Contains(KeyFor(word));
    }

    public static Stage GetStage(string word)
    {
        return IsLearned(word) ? Stage.Sign : Stage.Text;
    }

    /// Call at the start of every player turn, passing every word currently
    /// readable on screen. One of the ones still in text graduates, if it is
    /// time. Returns the word that changed, or null if nothing did.
    public static string OnTurnStarted(IList<string> wordsOnScreen)
    {
        _turn = Turn + 1;
        PlayerPrefs.SetInt(TurnKey, _turn);

        if (_turn < NextSignTurn) { PlayerPrefs.Save(); return null; }

        string picked = PickRandomUnlearned(wordsOnScreen);
        if (picked != null)
        {
            Learned.Add(KeyFor(picked));
            _nextSignTurn = _turn + Mathf.Max(1, TurnsPerNewSign);
            Save();
        }
        else
        {
            // Nothing left to teach from this hand; try again next turn rather
            // than burning the slot.
            _nextSignTurn = _turn + 1;
            Save();
        }
        return picked;
    }

    private static string PickRandomUnlearned(IList<string> words)
    {
        if (words == null || words.Count == 0) return null;

        var pool = new List<string>();
        for (int i = 0; i < words.Count; i++)
        {
            string w = words[i];
            if (string.IsNullOrEmpty(w) || IsLearned(w)) continue;
            if (!pool.Contains(w)) pool.Add(w);
        }
        if (pool.Count == 0) return null;

        return pool[Random.Range(0, pool.Count)];
    }

    /// Teach a word immediately, e.g. from a tutorial or while testing.
    public static void Learn(string word)
    {
        if (string.IsNullOrEmpty(word)) return;
        Learned.Add(KeyFor(word));
        Save();
    }

    public static void Forget(string word)
    {
        if (string.IsNullOrEmpty(word)) return;
        Learned.Remove(KeyFor(word));
        Save();
    }

    /// Back to all-text, turn zero.
    public static void ResetAll()
    {
        Learned.Clear();
        _turn = 0;
        _nextSignTurn = TurnsBeforeFirstSign + 1;
        PlayerPrefs.SetInt(TurnKey, 0);
        Save();
    }

    private static void Save()
    {
        PlayerPrefs.SetString(LearnedKey, string.Join("|", new List<string>(Learned).ToArray()));
        if (_nextSignTurn > 0) PlayerPrefs.SetInt(NextKey, _nextSignTurn);
        PlayerPrefs.Save();
    }
}

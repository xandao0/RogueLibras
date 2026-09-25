using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// JSON save slots under Application.persistentDataPath/saves/.
//
// Capture() snapshots the live run. Loading always goes through a fresh battle
// scene: LoadIntoBattle() puts the sign progression back straight away and
// leaves the rest as Pending, which CombatManager, CardManager and EnemyManager
// read while they set themselves up. Patching a battle that was already dealt
// could not work - the hand and draw pile had been built from the old deck, and
// the enemy had been rolled with the old difficulty.
public static class SaveSystem
{
    public const int SlotCount = 10;

    private static string SaveDir
    {
        get { return Path.Combine(Application.persistentDataPath, "saves"); }
    }

    public static string SlotPath(int slot)
    {
        return Path.Combine(SaveDir, "slot" + slot + ".json");
    }

    public static bool HasSave(int slot) { return File.Exists(SlotPath(slot)); }

    public static SaveData Load(int slot)
    {
        try
        {
            if (!HasSave(slot)) return null;
            string json = File.ReadAllText(SlotPath(slot));
            return JsonUtility.FromJson<SaveData>(json);
        }
        catch (Exception e)
        {
            Debug.LogError("[SaveSystem] Failed to read slot " + slot + ": " + e.Message);
            return null;
        }
    }

    /// The save written most recently, or null when there are none.
    public static SaveData LoadMostRecent()
    {
        SaveData newest = null;
        DateTime newestAt = DateTime.MinValue;

        for (int i = 0; i < SlotCount; i++)
        {
            var data = Load(i);
            if (data == null) continue;

            DateTime at = data.SavedAt;
            if (at == DateTime.MinValue) at = File.GetLastWriteTimeUtc(SlotPath(i));

            if (newest == null || at > newestAt)
            {
                newest = data;
                newestAt = at;
            }
        }
        return newest;
    }

    public static bool Write(int slot, SaveData data)
    {
        try
        {
            Directory.CreateDirectory(SaveDir);
            data.slot = slot;
            data.savedAtUtc = DateTime.UtcNow.ToString("o");
            File.WriteAllText(SlotPath(slot), JsonUtility.ToJson(data, true));
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("[SaveSystem] Failed to write slot " + slot + ": " + e.Message);
            return false;
        }
    }

    public static void Delete(int slot)
    {
        try { if (HasSave(slot)) File.Delete(SlotPath(slot)); }
        catch (Exception e) { Debug.LogError("[SaveSystem] Failed to delete slot " + slot + ": " + e.Message); }
    }

    /// Wipes every slot. Used by the settings screen's reset.
    public static int DeleteAll()
    {
        int removed = 0;
        for (int i = 0; i < SlotCount; i++)
        {
            if (!HasSave(i)) continue;
            Delete(i);
            if (!HasSave(i)) removed++;
        }
        return removed;
    }

    public static int UsedSlotCount()
    {
        int n = 0;
        for (int i = 0; i < SlotCount; i++) if (HasSave(i)) n++;
        return n;
    }

    /// Snapshot the current run. Returns null outside a battle (nothing to save).
    public static SaveData Capture()
    {
        if (CombatManager.instance == null || CardManager.instance == null) return null;

        var data = new SaveData();
        data.version = SaveData.CurrentVersion;
        data.sceneBuildIndex = SceneManager.GetActiveScene().buildIndex;

        data.currentHealth = CombatManager.instance.CurrentHealth;
        data.maxHealth = CombatManager.instance.maxHealth;
        data.currentBlock = CombatManager.instance.currentBlock;

        data.defeatedEnemies = EnemyManager.instance != null ? EnemyManager.instance.defeatedEnemiesCount : 0;

        data.deckCardNames = new List<string>();
        foreach (var c in CardManager.instance.currentAvailableCards)
            if (c != null) data.deckCardNames.Add(c.name);

        // Between fights (the reward screen) there is no enemy; loading that
        // save rolls a fresh one, just as picking the reward would have.
        Enemy enemy = CombatManager.instance.currentEnemy;
        if (enemy != null && enemy.eData != null && enemy.CurrentHP > 0)
        {
            data.enemyDataName = enemy.eData.name;
            data.enemyHP = enemy.CurrentHP;
        }

        data.learnedSigns = SignProgression.LearnedWords();
        data.signTurn = SignProgression.Turn;
        data.nextSignTurn = SignProgression.NextSignTurn;

        return data;
    }

    /// Resolves saved card names against every card the game knows about.
    /// Names that no longer exist are skipped with a warning.
    public static List<CardDataSo> ResolveDeck(List<string> names, List<CardDataSo> known)
    {
        var deck = new List<CardDataSo>();
        if (names == null || known == null) return deck;

        var byName = new Dictionary<string, CardDataSo>();
        foreach (var c in known)
            if (c != null && !byName.ContainsKey(c.name)) byName[c.name] = c;

        foreach (var n in names)
        {
            CardDataSo card;
            if (byName.TryGetValue(n, out card)) deck.Add(card);
            else Debug.LogWarning("[SaveSystem] Unknown card in save: " + n);
        }
        return deck;
    }

    // ---- starting a battle -------------------------------------------------

    /// Starts the battle scene from a save. The signs go back to what they were
    /// at once; everything else is picked up by the battle as it loads.
    public static void LoadIntoBattle(SaveData data, int battleSceneBuildIndex)
    {
        if (data == null)
        {
            StartNewRun(battleSceneBuildIndex);
            return;
        }

        if (data.HasSignProgress)
            SignProgression.Restore(data.learnedSigns, data.signTurn, data.nextSignTurn);

        _pending = data;
        LoadBattle(battleSceneBuildIndex);
    }

    /// A run from the very beginning: full health, starting deck, and every
    /// word back to plain Portuguese.
    public static void StartNewRun(int battleSceneBuildIndex)
    {
        _pending = null;
        SignProgression.ResetAll();
        LoadBattle(battleSceneBuildIndex);
    }

    /// Back to the most recent save, or to the very beginning if there is none.
    /// Returns true when a save was found.
    public static bool RestartFromLatest(int battleSceneBuildIndex)
    {
        SaveData latest = LoadMostRecent();
        if (latest != null) LoadIntoBattle(latest, battleSceneBuildIndex);
        else StartNewRun(battleSceneBuildIndex);
        return latest != null;
    }

    private static void LoadBattle(int battleSceneBuildIndex)
    {
        Time.timeScale = 1f;   // in case we are leaving from a paused screen
        SceneManager.LoadScene(battleSceneBuildIndex);
    }

    // ---- pending load, read by the battle scene as it starts ---------------

    private static SaveData _pending;

    /// The save the battle now loading should start from, or null for a fresh
    /// battle. Read it during Awake/Start; PendingSaveLoader lets go of it once
    /// the scene has finished setting up.
    public static SaveData Pending { get { return _pending; } }

    public static bool HasPending { get { return _pending != null; } }

    public static SaveData ConsumePending()
    {
        var d = _pending;
        _pending = null;
        return d;
    }
}

using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

// Maps a plain word ("Fogo", "Maçã", "Carta Comum") to the SignWriting sign
// sprites. A single word migt need a combinations of signs - "carta comum"
// has two - so every entry holds an ordered array.
//
// One library per sign language. It sits in that language's locale folder,
// Assets/Locales/<id>/, next to its dictionary.json and its signs/ folder of
// SVGs, and LocaleSettings points at it (Loc.Signs is the active one).
//
// Words that share a sign (ataque, ataca -> Dano) are synonyms: the defaults
// below, plus the "sinonimos" in the dictionary.
//
// Rebuild the table from the SVGs with RogueLibras > SignWriting > Atualizar
// sinais (or the asset's context menu).
[CreateAssetMenu(fileName = "SignWritingLibrary", menuName = "RogueLibras/SignWriting Library")]
public class SignWritingLibrary : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        [Tooltip("Normalized lookup key: lowercase, accents stripped, spaces as underscores.")]
        public string key;

        [Tooltip("The signs that spell this word, in reading order.")]
        public Sprite[] signs;

        [Tooltip("The same signs' names in the TMP sprite asset, for drawing " +
                 "them inline in a description (e.g. Carta_Comum_1, Carta_Comum_2).")]
        public string[] spriteNames;
    }

    [System.Serializable]
    public class Alias
    {
        [Tooltip("A word as it is written, e.g. \"ataque\".")]
        public string word;
        [Tooltip("The word whose sign it uses, e.g. \"Dano\".")]
        public string key;
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    [Tooltip("Words that are signed with another word's sign.")]
    [SerializeField] private List<Alias> aliases = new List<Alias>();

    // Always there, so descriptions keep working on a library that has not
    // been rebuilt since the synonyms moved into the dictionary file.
    private static readonly string[,] DefaultAliases =
    {
        { "ataque", "dano" }, { "ataca", "dano" },
        { "cura", "curar" }, { "curam", "curar" },
        { "defende", "defesa" }, { "defender", "defesa" },
    };

    private Dictionary<string, Sprite[]> _lookup;
    private Dictionary<string, string[]> _spriteNames;
    private Dictionary<string, string> _aliases;

    // Lowercase, strip diacritics (Maçã -> maca), collapse separators to '_'.
    public static string Normalize(string raw)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;

        string decomposed = raw.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (char c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (c == ' ' || c == '-') { sb.Append('_'); continue; }
            if (char.IsLetterOrDigit(c) || c == '_') sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private void BuildLookup()
    {
        _lookup = new Dictionary<string, Sprite[]>();
        _spriteNames = new Dictionary<string, string[]>();
        foreach (var e in entries)
        {
            if (e == null || string.IsNullOrEmpty(e.key) || e.signs == null || e.signs.Length == 0) continue;
            _lookup[e.key] = e.signs;
            if (e.spriteNames != null && e.spriteNames.Length == e.signs.Length) _spriteNames[e.key] = e.spriteNames;
        }

        _aliases = new Dictionary<string, string>();
        for (int i = 0; i < DefaultAliases.GetLength(0); i++)
            _aliases[DefaultAliases[i, 0]] = DefaultAliases[i, 1];
        foreach (var a in aliases)
            if (a != null && !string.IsNullOrEmpty(a.word) && !string.IsNullOrEmpty(a.key))
                _aliases[Normalize(a.word)] = Normalize(a.key);
    }

    /// The key of the sign a word is written with - itself, or the word it is
    /// a synonym of - or null when there is no sign for it.
    public string Resolve(string word)
    {
        if (_lookup == null) BuildLookup();
        string key = Normalize(word);
        if (string.IsNullOrEmpty(key)) return null;
        if (_lookup.ContainsKey(key)) return key;

        string target;
        if (_aliases.TryGetValue(key, out target) && _lookup.ContainsKey(target)) return target;
        return null;
    }

    /// Returns the signs for a word, or null when the word has no sign yet.
    public Sprite[] Get(string word)
    {
        string key = Resolve(word);
        return key != null ? _lookup[key] : null;
    }

    public bool Has(string word) { return Resolve(word) != null; }

    /// The word's signs by their names in the TMP sprite asset, in reading
    /// order. Falls back to the sprites' own names, which match the SVG files.
    public string[] SpriteNames(string word)
    {
        string key = Resolve(word);
        if (key == null) return null;

        string[] names;
        if (_spriteNames.TryGetValue(key, out names)) return names;

        Sprite[] signs = _lookup[key];
        names = new string[signs.Length];
        for (int i = 0; i < signs.Length; i++) names[i] = signs[i] != null ? signs[i].name : key;
        return names;
    }

#if UNITY_EDITOR
    /// This sign language's locale folder: the folder the library sits in.
    public string LocaleFolder
    {
        get { return System.IO.Path.GetDirectoryName(UnityEditor.AssetDatabase.GetAssetPath(this)).Replace('\\', '/'); }
    }

    /// Where its sign SVGs live, one subfolder per category.
    public string SignFolder { get { return LocaleFolder + "/signs"; } }

    /// FSW codes, synonyms and words left unsigned - the Node script's input.
    public string DictionaryPath { get { return LocaleFolder + "/dictionary.json"; } }

    [ContextMenu("Rebuild From SignWriting Folder")]
    private void RebuildFromFolderMenu()
    {
        RebuildFromFolder();
    }

    /// Rebuilds the table from every SVG under the signs folder, and the
    /// synonyms from the dictionary file. Returns how many words it found.
    public int RebuildFromFolder()
    {
        var grouped = new Dictionary<string, SortedList<int, Sprite>>();

        foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:Sprite", new[] { SignFolder }))
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".svg")) continue;

            var sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) continue;

            string file = System.IO.Path.GetFileNameWithoutExtension(path);
            bool isNumberFolder = path.Contains("/numbers/");

            // "Carta_Comum_1" -> key "carta_comum", order 1. Numbers keep their
            // suffix (Numero_3 is its own sign, not part of a multi-sign word).
            string baseName = file;
            int order = 0;
            if (!isNumberFolder)
            {
                var m = System.Text.RegularExpressions.Regex.Match(file, @"^(.*?)_(\d+)$");
                if (m.Success)
                {
                    baseName = m.Groups[1].Value;
                    order = int.Parse(m.Groups[2].Value);
                }
            }

            string key = Normalize(baseName);
            if (!grouped.ContainsKey(key)) grouped[key] = new SortedList<int, Sprite>();
            int slot = order;
            while (grouped[key].ContainsKey(slot)) slot++;
            grouped[key].Add(slot, sprite);
        }

        entries = new List<Entry>();
        foreach (var kv in grouped)
        {
            var list = new List<Sprite>(kv.Value.Values);
            var names = new string[list.Count];
            for (int i = 0; i < list.Count; i++)
                names[i] = System.IO.Path.GetFileNameWithoutExtension(UnityEditor.AssetDatabase.GetAssetPath(list[i]));
            entries.Add(new Entry { key = kv.Key, signs = list.ToArray(), spriteNames = names });
        }
        entries.Sort((a, b) => string.CompareOrdinal(a.key, b.key));

        aliases = ReadAliases();

        _lookup = null;
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[SignWritingLibrary] Rebuilt: " + entries.Count + " entries, " + aliases.Count +
                  " synonyms from " + SignFolder);
        return entries.Count;
    }

    // The "sinonimos" object of the dictionary file: { "ataque": "Dano", ... }.
    // JsonUtility cannot read a map, and this is the only part needed here.
    private List<Alias> ReadAliases()
    {
        var list = new List<Alias>();
        if (!System.IO.File.Exists(DictionaryPath)) return list;

        string json = System.IO.File.ReadAllText(DictionaryPath);
        var block = System.Text.RegularExpressions.Regex.Match(json, "\"sinonimos\"\\s*:\\s*\\{([^}]*)\\}");
        if (!block.Success) return list;

        var pairs = System.Text.RegularExpressions.Regex.Matches(block.Groups[1].Value, "\"([^\"]+)\"\\s*:\\s*\"([^\"]+)\"");
        foreach (System.Text.RegularExpressions.Match m in pairs)
            list.Add(new Alias { word = m.Groups[1].Value, key = m.Groups[2].Value });
        return list;
    }
#endif
}

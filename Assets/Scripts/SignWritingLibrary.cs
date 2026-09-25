using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

// Maps a plain word ("Fogo", "Maçã", "Carta Comum") to the SignWriting sign
// sprites. A single word migt need a combinations of signs - "carta comum"
// has two - so every entry holds an ordered array.
//
// Rebuild the table from the SVGs in Assets/SignWriting via the asset's
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
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    private Dictionary<string, Sprite[]> _lookup;

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
        foreach (var e in entries)
        {
            if (e == null || string.IsNullOrEmpty(e.key) || e.signs == null || e.signs.Length == 0) continue;
            _lookup[e.key] = e.signs;
        }
    }

    /// Returns the signs for a word, or null when the word has no sign yet.
    public Sprite[] Get(string word)
    {
        if (_lookup == null) BuildLookup();
        string key = Normalize(word);
        if (string.IsNullOrEmpty(key)) return null;
        Sprite[] signs;
        return _lookup.TryGetValue(key, out signs) ? signs : null;
    }

    public bool Has(string word) { return Get(word) != null; }

    // How a sign reads in Portuguese, for keys that lost their accents or
    // spacing on the way in ("maca" is Maçã). Anything not listed reads as its
    // key with the underscores turned back into spaces.
    private static readonly Dictionary<string, string> PortugueseNames = new Dictionary<string, string>
    {
        { "agua", "Água" },
        { "maca", "Maçã" },
        { "furacao", "Furacão" },
        { "pao", "Pão" },
        { "combinacao", "Combinação" },
        { "carta_comum", "Carta Comum" },
        { "encerrar_turno", "Encerrar Turno" },
    };

    private static readonly string[] NumberNames =
        { "zero", "um", "dois", "três", "quatro", "cinco", "seis", "sete", "oito", "nove", "dez" };

    /// The Portuguese for a sign's word: "maca" -> "Maçã", "Numero_3" -> "3 (três)".
    public static string ToPortuguese(string word)
    {
        string key = Normalize(word);
        if (string.IsNullOrEmpty(key)) return string.Empty;

        string named;
        if (PortugueseNames.TryGetValue(key, out named)) return named;

        const string numberPrefix = "numero_";
        int n;
        if (key.StartsWith(numberPrefix) && int.TryParse(key.Substring(numberPrefix.Length), out n))
            return n >= 0 && n < NumberNames.Length ? n + " (" + NumberNames[n] + ")" : n.ToString();

        string[] parts = key.Split('_');
        for (int i = 0; i < parts.Length; i++)
            if (parts[i].Length > 0) parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
        return string.Join(" ", parts);
    }

#if UNITY_EDITOR
    [ContextMenu("Rebuild From SignWriting Folder")]
    private void RebuildFromFolder()
    {
        const string root = "Assets/SignWriting";
        var grouped = new Dictionary<string, SortedList<int, Sprite>>();

        foreach (var guid in UnityEditor.AssetDatabase.FindAssets("t:Sprite", new[] { root }))
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
            entries.Add(new Entry { key = kv.Key, signs = list.ToArray() });
        }
        entries.Sort((a, b) => string.CompareOrdinal(a.key, b.key));

        _lookup = null;
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[SignWritingLibrary] Rebuilt: " + entries.Count + " entries from " + root);
    }
#endif
}

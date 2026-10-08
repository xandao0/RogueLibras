using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Checks how complete each language is, against the reference text (pt-BR):
//
//   RogueLibras > Localização > Relatório de tradução
//     writes Assets/Locales/translation-report.txt with, for every sign
//     language, the words the game shows that have no sign yet (missing
//     SVGs), the texts it doesn't sign at all, and glosses pointing at keys
//     that don't exist; and for every other text language, the keys it has
//     not translated yet.
//
// Card text is glossed word by word, so its words count as "shown"; menu text
// only counts when the sign language lists it in its glosses.json.
public static class LocalizationTools
{
    private const string ReportPath = "Assets/Locales/translation-report.txt";

    // Keys that are data for other texts rather than text shown on its own.
    private static readonly string[] NotShownPrefixes =
        { "_meta.", "cards.", "enemies.", "effects.", "words.", "numbers.", "save.", "start.example_word" };

    [MenuItem("RogueLibras/Localização/Relatório de tradução")]
    public static void WriteReportMenu()
    {
        string summary = WriteReport();
        EditorUtility.DisplayDialog("Localização", summary, "OK");
    }

    /// Writes the report and returns a short summary of it.
    public static string WriteReport()
    {
        var settings = Resources.Load<LocaleSettings>("LocaleSettings");
        if (settings == null) return "Não encontrei Assets/Locales/Resources/LocaleSettings.";

        Dictionary<string, string> reference = Read(settings.FindText(settings.referenceText));
        var sb = new StringBuilder();
        var summary = new StringBuilder();
        sb.AppendLine("Relatório de tradução - " + System.DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
        sb.AppendLine("Referência: " + settings.referenceText + " (" + reference.Count + " textos)");
        sb.AppendLine();

        foreach (var sign in settings.signLocales)
        {
            if (sign == null) continue;
            sb.AppendLine("=== " + sign.displayName + " (" + sign.id + ") ===");

            List<string> missing = sign.library != null ? MissingSigns(sign.library, sign, reference) : new List<string>();
            sb.AppendLine();
            sb.AppendLine("Sinais que faltam (" + missing.Count + ") - crie no SignMaker e cole o FSW em dictionary.json:");
            foreach (var m in missing) sb.AppendLine("  " + m);

            // Every text the menus show, and how far its translation goes. In
            // Libras mode only the complete ones are shown as signs; the rest
            // stay in Portuguese.
            Dictionary<string, string> glosses = Read(sign.glosses);
            HashSet<string> ignored = sign.library != null ? ReadIgnored(sign.library) : new HashSet<string>();
            var done = new List<string>();
            var partial = new List<string>();
            var untranslated = new List<string>();
            foreach (var k in reference.Keys.Where(IsShown).OrderBy(k => k))
            {
                string gloss;
                if (glosses.TryGetValue(k, out gloss) && !string.IsNullOrEmpty(gloss))
                {
                    var missingEntries = gloss.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries)
                        .Where(e => sign.library == null || !sign.library.Has(e)).ToList();
                    if (missingEntries.Count == 0) done.Add("  " + k + "  =  " + OneLine(reference[k]) + "   [" + gloss + "]");
                    else partial.Add("  " + k + "  =  " + OneLine(reference[k]) + "   [" + gloss + "]  falta: " + string.Join(", ", missingEntries));
                    continue;
                }

                // No gloss yet: which of its words could already be signed.
                var words = Regex.Matches(Regex.Replace(reference[k], "<[^>]+>|\\{\\d+\\}", " "), @"[\p{L}\p{N}]+")
                    .Cast<Match>().Select(m => m.Value)
                    .Where(w => !ignored.Contains(SignWritingLibrary.Normalize(w))).ToList();
                System.Func<string, bool> hasSign = w => sign.library != null &&
                    sign.library.Has(Regex.IsMatch(w, @"^\d+$") ? "Numero_" + w : w);
                var have = words.Where(hasSign).Distinct().ToList();
                var lack = words.Where(w => !hasSign(w)).Distinct().ToList();
                untranslated.Add("  " + k + "  =  " + OneLine(reference[k])
                    + (have.Count > 0 ? "   | já têm sinal: " + string.Join(", ", have) : "")
                    + (lack.Count > 0 ? "   | sem sinal: " + string.Join(", ", lack) : ""));
            }

            sb.AppendLine();
            sb.AppendLine("Textos traduzidos em " + sign.displayName + " (" + done.Count + ") - aparecem em sinais no modo Libras:");
            foreach (var l in done) sb.AppendLine(l);
            sb.AppendLine();
            sb.AppendLine("Tradução incompleta (" + partial.Count + ") - a glosa existe mas falta sinal; ficam em português até completar:");
            foreach (var l in partial) sb.AppendLine(l);
            sb.AppendLine();
            sb.AppendLine("Sem tradução (" + untranslated.Count + ") - ficam em português; para traduzir, adicione a chave em glosses.json:");
            foreach (var l in untranslated) sb.AppendLine(l);
            var unsigned = untranslated;

            var orphan = glosses.Keys.Where(k => !k.StartsWith("_meta.") && !reference.ContainsKey(k)).OrderBy(k => k).ToList();
            if (orphan.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Glosas para chaves que não existem em " + settings.referenceText + " (" + orphan.Count + "):");
                foreach (var k in orphan) sb.AppendLine("  " + k);
            }
            sb.AppendLine();

            summary.AppendLine(sign.displayName + ": " + done.Count + " textos traduzidos, " + partial.Count + " incompletos, "
                               + unsigned.Count + " sem tradução; " + missing.Count + " sinais faltando.");
        }

        foreach (var text in settings.textLocales)
        {
            if (text == null || text.id == settings.referenceText) continue;
            Dictionary<string, string> strings = Read(text);
            var untranslated = reference.Keys.Where(k => !k.StartsWith("_meta.") && !strings.ContainsKey(k)).OrderBy(k => k).ToList();

            sb.AppendLine("=== " + text.displayName + " (" + text.id + ") ===");
            sb.AppendLine("Textos sem tradução (" + untranslated.Count + "):");
            foreach (var k in untranslated) sb.AppendLine("  " + k + "  =  " + OneLine(reference[k]));
            sb.AppendLine();

            summary.AppendLine(text.displayName + ": " + untranslated.Count + " textos sem tradução.");
        }

        File.WriteAllText(ReportPath, sb.ToString(), new UTF8Encoding(false));
        AssetDatabase.ImportAsset(ReportPath);
        Debug.Log("[Localização] " + summary.ToString().Replace("\n", " ") + "Veja " + ReportPath);
        return summary + "\nDetalhes em " + ReportPath;
    }

    /// The words the game shows that the default sign language has no sign
    /// for, each with where it is used.
    public static List<string> MissingSigns(SignWritingLibrary library)
    {
        var settings = Resources.Load<LocaleSettings>("LocaleSettings");
        if (settings == null) return new List<string>();
        var sign = settings.signLocales.FirstOrDefault(s => s != null && s.library == library);
        return MissingSigns(library, sign, Read(settings.FindText(settings.referenceText)));
    }

    private static List<string> MissingSigns(SignWritingLibrary library, LocaleSettings.SignLocale sign,
                                             Dictionary<string, string> reference)
    {
        HashSet<string> ignored = ReadIgnored(library);
        var missing = new SortedDictionary<string, SortedSet<string>>(System.StringComparer.OrdinalIgnoreCase);

        System.Action<string, string> need = (word, where) =>
        {
            if (string.IsNullOrEmpty(word) || library.Has(word) || HasDigits(library, word)) return;
            if (!missing.ContainsKey(word)) missing[word] = new SortedSet<string>();
            missing[word].Add(where);
        };

        // Menus: whatever glosses.json says to sign.
        if (sign != null)
            foreach (var kv in Read(sign.glosses))
            {
                if (kv.Key.StartsWith("_meta.")) continue;
                foreach (var entry in kv.Value.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries))
                    need(entry, kv.Key);
            }

        // Cards: the name and every word of the description, as the text reads.
        foreach (var kv in reference)
        {
            if (kv.Key.StartsWith("cards.") && kv.Key.EndsWith(".name")) need(kv.Value, kv.Key);
            else if (kv.Key.StartsWith("enemies.") && kv.Key.EndsWith(".name")) need(kv.Value, kv.Key);
            else if (kv.Key.StartsWith("cards.") && kv.Key.EndsWith(".description"))
                foreach (string raw in kv.Value.Split(' '))
                {
                    string word = Regex.Replace(raw, @"[^\p{L}\p{N}]", "");
                    if (word.Length == 0) continue;
                    if (word.Length == 1 && "SDCX".IndexOf(char.ToUpperInvariant(word[0])) >= 0) continue;   // the card's numbers
                    if (ignored.Contains(SignWritingLibrary.Normalize(word))) continue;
                    int n;
                    need(int.TryParse(word, out n) ? "Numero_" + n : word, kv.Key);
                }
        }

        // The numbers the cards and enemies put on screen.
        foreach (string guid in AssetDatabase.FindAssets("t:CardDataSo"))
        {
            var card = AssetDatabase.LoadAssetAtPath<CardDataSo>(AssetDatabase.GUIDToAssetPath(guid));
            if (card == null) continue;
            foreach (int v in new[] { card.cardStamina, card.strength, card.defense, card.cure, card.cardDrawAmount })
                if (v > 0) need("Numero_" + v, "cards." + card.name);
        }
        foreach (string guid in AssetDatabase.FindAssets("t:EnemyDataSO"))
        {
            var enemy = AssetDatabase.LoadAssetAtPath<EnemyDataSO>(AssetDatabase.GUIDToAssetPath(guid));
            if (enemy == null) continue;
            foreach (char d in enemy.maxHP.ToString()) need("Numero_" + d, "enemies." + enemy.name + " (vida)");
        }

        return missing.Select(kv => kv.Key + "  (" + string.Join(", ", kv.Value) + ")").ToList();
    }

    // A number with no sign of its own still shows when all its digits have
    // one (see SignText.Number).
    private static bool HasDigits(SignWritingLibrary library, string word)
    {
        var m = Regex.Match(word, @"^Numero_(\d{2,})$", RegexOptions.IgnoreCase);
        return m.Success && m.Groups[1].Value.All(c => library.Has("Numero_" + c));
    }

    private static bool IsShown(string key)
    {
        foreach (var p in NotShownPrefixes) if (key.StartsWith(p)) return false;
        return true;
    }

    private static Dictionary<string, string> Read(LocaleSettings.TextLocale locale)
    {
        return locale != null ? Read(locale.strings) : new Dictionary<string, string>();
    }

    private static Dictionary<string, string> Read(TextAsset file)
    {
        if (file == null) return new Dictionary<string, string>();
        try { return LocaleJson.Flatten(file.text); }
        catch (System.FormatException e)
        {
            Debug.LogError("[Localização] " + AssetDatabase.GetAssetPath(file) + ": " + e.Message);
            return new Dictionary<string, string>();
        }
    }

    // The dictionary's "ignorar": [ "de", "e", ... ] - words left unsigned on purpose.
    private static HashSet<string> ReadIgnored(SignWritingLibrary library)
    {
        var set = new HashSet<string>();
        if (!File.Exists(library.DictionaryPath)) return set;

        var block = Regex.Match(File.ReadAllText(library.DictionaryPath), "\"ignorar\"\\s*:\\s*\\[([^\\]]*)\\]");
        if (!block.Success) return set;
        foreach (Match m in Regex.Matches(block.Groups[1].Value, "\"([^\"]+)\""))
            set.Add(SignWritingLibrary.Normalize(m.Groups[1].Value));
        return set;
    }

    private static string OneLine(string s)
    {
        return s.Replace("\n", " / ");
    }
}

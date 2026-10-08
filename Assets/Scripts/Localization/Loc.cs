using System;
using System.Collections.Generic;
using UnityEngine;

// Every piece of text the player sees comes through here, by key:
//
//   Loc.T("menu.play")                 -> "Jogar"
//   Loc.T("settings.slot", 3)          -> "Save 3"
//   Loc.Signed("menu.play")            -> "<sign of Jogar>  Jogar"
//   Loc.Gloss("menu.play")             -> "Jogar" (the dictionary entry signed)
//
// The texts live in Assets/Locales/<lang>/strings.json and the signs in
// Assets/Locales/<sign language>/glosses.json; LocaleSettings lists them. A key
// missing from the active language falls back to the reference one (pt-BR),
// and one missing everywhere shows as [key] so it is easy to spot.
public static class Loc
{
    public static event Action Changed;

    private static LocaleSettings _settings;
    private static Dictionary<string, string> _text;
    private static Dictionary<string, string> _reference;
    private static Dictionary<string, string> _glosses;
    private static LocaleSettings.SignLocale _sign;
    private static string _textId;

    private static LocaleSettings Settings
    {
        get
        {
            if (_settings == null) _settings = Resources.Load<LocaleSettings>("LocaleSettings");
            return _settings;
        }
    }

    /// The written language in use, e.g. "pt-BR".
    public static string TextLocale { get { Load(); return _textId; } }

    /// The sign language in use, or null when there is none.
    public static LocaleSettings.SignLocale SignLocale { get { Load(); return _sign; } }

    /// The active sign language's sign sprites.
    public static SignWritingLibrary Signs { get { Load(); return _sign != null ? _sign.library : null; } }

    /// Switches language (for a future language menu). Null keeps the current one.
    public static void Use(string textId, string signId)
    {
        _text = null;
        Load(textId, signId);
        if (Changed != null) Changed();
    }

    /// Re-reads the files, e.g. after editing them in the editor.
    public static void Reload()
    {
        Use(_textId, _sign != null ? _sign.id : null);
    }

    private static void Load(string textId = null, string signId = null)
    {
        if (_text != null) return;

        _text = new Dictionary<string, string>();
        _reference = new Dictionary<string, string>();
        _glosses = new Dictionary<string, string>();

        LocaleSettings s = Settings;
        if (s == null)
        {
            Debug.LogError("[Loc] No LocaleSettings in a Resources folder - every text will show as its key.");
            _textId = "";
            return;
        }

        _textId = textId ?? _textId ?? s.defaultText;
        var text = s.FindText(_textId) ?? s.FindText(s.defaultText);
        if (text != null) { _textId = text.id; _text = Read(text.strings); }

        var reference = s.FindText(s.referenceText);
        _reference = reference != null && reference != text ? Read(reference.strings) : _text;

        _sign = s.FindSign(signId ?? (_sign != null ? _sign.id : s.defaultSign));
        if (_sign != null) _glosses = Read(_sign.glosses);
    }

    private static Dictionary<string, string> Read(TextAsset file)
    {
        if (file == null) return new Dictionary<string, string>();
        try { return LocaleJson.Flatten(file.text); }
        catch (FormatException e)
        {
            Debug.LogError("[Loc] " + file.name + ": " + e.Message);
            return new Dictionary<string, string>();
        }
    }

    // ---- text ---------------------------------------------------------------------

    public static bool Has(string key)
    {
        Load();
        return _text.ContainsKey(key) || _reference.ContainsKey(key);
    }

    /// The text for a key in the active language.
    public static string T(string key)
    {
        Load();
        string v;
        if (_text.TryGetValue(key, out v) || _reference.TryGetValue(key, out v)) return v;
        return "[" + key + "]";
    }

    public static string T(string key, params object[] args)
    {
        string format = T(key);
        try { return string.Format(format, args); }
        catch (FormatException) { return format; }
    }

    /// The text for a key, or the given fallback when no language has it.
    public static string TOr(string key, string fallback)
    {
        Load();
        string v;
        if (_text.TryGetValue(key, out v) || _reference.TryGetValue(key, out v)) return v;
        return fallback;
    }

    // ---- signs --------------------------------------------------------------------

    /// The sign(s) for a key in the active sign language - dictionary entries
    /// separated by spaces - or null when the text is not signed.
    public static string Gloss(string key)
    {
        Load();
        string v;
        return _glosses.TryGetValue(key, out v) && !string.IsNullOrEmpty(v) ? v : null;
    }

    // ---- display mode: Portuguese or Libras ----------------------------------------

    public enum DisplayMode
    {
        Text = 0,    // the written language (pt-BR), with signs beside it where a menu has them
        Signs = 1    // the sign language instead of text, wherever it has the whole translation
    }

    private const string DisplayKey = "settings.display";
    private static int _display = -1;

    /// How the menus read: Português or Libras. The language button toggles it.
    public static DisplayMode Display
    {
        get
        {
            if (_display < 0) _display = PlayerPrefs.GetInt(DisplayKey, (int)DisplayMode.Text);
            return (DisplayMode)_display;
        }
        set
        {
            _display = (int)value;
            PlayerPrefs.SetInt(DisplayKey, _display);
            PlayerPrefs.Save();
            if (Changed != null) Changed();
        }
    }

    public static void ToggleDisplay()
    {
        Display = Display == DisplayMode.Signs ? DisplayMode.Text : DisplayMode.Signs;
    }

    /// A text as the player has chosen to read it: in Libras mode its signs,
    /// when the sign language has every sign of its translation; otherwise -
    /// and always in Português mode - the written text.
    public static string Text(string key, float scale = 1.6f)
    {
        if (Display == DisplayMode.Signs)
        {
            string signs = SignText.AllSignsFor(Gloss(key), scale);
            if (signs != null) return signs;
        }
        return T(key);
    }

    /// Menus and buttons. Português mode: the text, with its signs beside it
    /// when the sign language has them. Libras mode: the signs instead of the
    /// text (bigger), or the text when the translation is not complete.
    public static string Signed(string key, SignText.Layout layout = SignText.Layout.Inline, float scale = 1.3f)
    {
        if (Display == DisplayMode.Signs)
        {
            string signs = SignText.AllSignsFor(Gloss(key), scale * 1.3f);
            if (signs != null) return signs;
            return T(key);
        }
        return SignText.Bilingual(T(key), Gloss(key), layout, scale);
    }

    // ---- game content ------------------------------------------------------------
    // Cards, enemies and effects are keyed by their asset name. The asset's own
    // field is the fallback, so a new card shows its authored text until it is
    // added to the locale files (RogueLibras > Localização > Sincronizar textos).

    public static string CardName(CardDataSo card)
    {
        return card == null ? "" : TOr("cards." + card.name + ".name", card.cardName);
    }

    public static string CardDescription(CardDataSo card)
    {
        return card == null ? "" : TOr("cards." + card.name + ".description", card.cardDescription);
    }

    public static string EnemyName(EnemyDataSO enemy)
    {
        return enemy == null ? "" : TOr("enemies." + enemy.name + ".name", enemy.enemyName);
    }

    public static string EffectName(EffectDataSO effect)
    {
        return effect == null ? "" : TOr("effects." + effect.name + ".name", effect.effectName);
    }

    public static string EffectDescription(EffectDataSO effect)
    {
        return effect == null ? "" : TOr("effects." + effect.name + ".description", effect.effectDesc);
    }

    /// How a sign's dictionary entry reads in the active language: "maca" ->
    /// "Maçã", "Numero_3" -> "3 (três)". Anything not listed under "words"
    /// reads as the entry with its underscores turned into spaces.
    public static string WordForSign(string signKey)
    {
        string key = SignWritingLibrary.Normalize(signKey);
        if (string.IsNullOrEmpty(key)) return string.Empty;

        string named = TOr("words." + key, null);
        if (named != null) return named;

        const string numberPrefix = "numero_";
        int n;
        if (key.StartsWith(numberPrefix) && int.TryParse(key.Substring(numberPrefix.Length), out n))
        {
            string name = TOr("numbers." + n, null);
            return name != null ? T("numbers.format", n, name) : n.ToString();
        }

        string[] parts = key.Split('_');
        for (int i = 0; i < parts.Length; i++)
            if (parts[i].Length > 0) parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1);
        return string.Join(" ", parts);
    }
}

using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

// Turns words and numbers into TMP rich text that draws them as SignWriting,
// for anything outside the cards: the HUD counters, the enemy, the menus.
//
//   Staged / Number  - follow SignProgression: written or signed depending on
//                      how far the run has got (and which direction it goes).
//   Bilingual        - always both, the signs and the Portuguese, for buttons
//                      and menus that have to be readable by everyone at once.
//
// Signs come from the active sign language (Loc.Signs) and are drawn with TMP
// inline sprites from the SignWriting sprite asset (rebuilt by RogueLibras >
// SignWriting > Atualizar sinais). Anything without a sign just stays written.
public static class SignText
{
    /// The sign sprites without the bold stroke and ring, for big HUD text
    /// (the outline that keeps small card signs readable smears big ones).
    /// Rebuilt next to the default sign sprite asset by Atualizar sinais.
    public const string PlainSpriteAssetName = "SignWritingPlain";

    private static TMP_SpriteAsset _plain;
    private static bool _plainLooked;

    /// Draws this text's signs with the plain sprites. Cheap to call often.
    public static void UsePlainSigns(TMP_Text text)
    {
        if (text == null) return;
        if (!_plainLooked)
        {
            _plainLooked = true;
            _plain = Resources.Load<TMP_SpriteAsset>("Sprite Assets/" + PlainSpriteAssetName);
        }
        if (_plain != null && text.spriteAsset != _plain) text.spriteAsset = _plain;
    }

    /// Inline sprite tags for a word's signs, or null when it has none.
    public static string Tags(string word, float scale = 1f)
    {
        SignWritingLibrary library = Loc.Signs;
        string[] names = library != null ? library.SpriteNames(word) : null;
        if (names == null || names.Length == 0 || !InAtlas(names)) return null;
        return Wrap(names, scale);
    }

    /// The word as the run currently shows it: its sign once it has
    /// graduated (or before, going Libras to Portuguese), otherwise the text.
    public static string Staged(string word, string written, float scale = 1f)
    {
        if (SignProgression.GetStage(word) != SignProgression.Stage.Sign) return written;
        return Tags(word, scale) ?? written;
    }

    /// A number as the run currently shows it. Numbers with a sign of their
    /// own use it; longer ones are spelled digit by digit, the way Libras
    /// signs most numbers above ten, and switch once all their digits have.
    public static string Number(int value, float scale = 1f)
    {
        string plain = value.ToString();
        if (value < 0) return plain;

        string own = "Numero_" + value;
        if (Tags(own) != null) return Staged(own, plain, scale);

        var digits = new List<string>();
        foreach (char c in plain) digits.Add("Numero_" + c);
        foreach (var d in digits)
            if (SignProgression.GetStage(d) != SignProgression.Stage.Sign || Tags(d) == null) return plain;

        var sb = new StringBuilder();
        foreach (var d in digits) sb.Append(Tags(d, scale));
        return sb.ToString();
    }

    /// The words a number puts on screen, for the turn's graduation pool.
    public static void NumberWords(int value, List<string> into)
    {
        if (value < 0) return;
        string own = "Numero_" + value;
        if (Tags(own) != null) { AddWord(into, own); return; }
        foreach (char c in value.ToString()) AddWord(into, "Numero_" + c);
    }

    /// Adds a word to the pool if it has a sign to graduate to.
    public static void AddWord(List<string> into, string word)
    {
        if (string.IsNullOrEmpty(word) || Tags(word) == null) return;
        foreach (var w in into)
            if (SignWritingLibrary.Normalize(w) == SignWritingLibrary.Normalize(word)) return;
        into.Add(word);
    }

    // ---- bilingual ------------------------------------------------------------

    public enum Layout
    {
        Inline,   // [sign] Palavra
        Above     // [sign] on its own line, the word under it
    }

    /// A text and its signs together. The gloss is what the sign language
    /// signs for it - dictionary entries separated by spaces, see Loc.Gloss.
    /// No gloss, or none of it has a sign yet: just the text.
    public static string Bilingual(string text, string gloss, Layout layout = Layout.Inline, float scale = 1.3f)
    {
        if (string.IsNullOrEmpty(text)) return text;
        string signs = SignsFor(gloss, scale);
        if (signs == null) return text;
        return layout == Layout.Above ? signs + "\n" + text : signs + "  " + text;
    }

    /// The signs for a whole gloss, or null unless every entry has a sign -
    /// for showing a text in the sign language instead of writing it, where
    /// half a translation would be worse than the Portuguese.
    public static string AllSignsFor(string gloss, float scale = 1.3f)
    {
        if (string.IsNullOrEmpty(gloss)) return null;

        var sb = new StringBuilder();
        foreach (var entry in gloss.Split(new[] { ' ', '\n' }, System.StringSplitOptions.RemoveEmptyEntries))
        {
            string tags = Tags(entry, 1f);
            if (tags == null) return null;
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(tags);
        }
        if (sb.Length == 0) return null;
        return "<size=" + Mathf.RoundToInt(scale * 100f) + "%>" + sb + "</size>";
    }

    /// The signs for a gloss, or null when none of its entries has a sign.
    public static string SignsFor(string gloss, float scale = 1.3f)
    {
        if (string.IsNullOrEmpty(gloss)) return null;

        var sb = new StringBuilder();
        foreach (var entry in gloss.Split(new[] { ' ', '\n' }, System.StringSplitOptions.RemoveEmptyEntries))
        {
            string tags = Tags(entry, 1f);
            if (tags == null) continue;
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(tags);
        }
        if (sb.Length == 0) return null;
        return "<size=" + Mathf.RoundToInt(scale * 100f) + "%>" + sb + "</size>";
    }

    // ---- helpers ----------------------------------------------------------------

    private static string Wrap(string[] names, float scale)
    {
        var sb = new StringBuilder();
        bool sized = !Mathf.Approximately(scale, 1f);
        if (sized) sb.Append("<size=").Append(Mathf.RoundToInt(scale * 100f)).Append("%>");
        // tint=1: the atlas is a white mask, so the sign takes the text colour.
        foreach (var n in names) sb.Append("<sprite name=\"").Append(n).Append("\" tint=1>");
        if (sized) sb.Append("</size>");
        return sb.ToString();
    }

    private static bool InAtlas(string[] names)
    {
        TMP_SpriteAsset sa = TMP_Settings.defaultSpriteAsset;
        if (sa == null) return false;
        foreach (var n in names)
            if (sa.GetSpriteIndexFromName(n) < 0) return false;
        return true;
    }
}

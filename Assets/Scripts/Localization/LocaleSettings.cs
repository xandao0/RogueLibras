using System.Collections.Generic;
using UnityEngine;

// The languages the game knows, and which ones are in use. Lives at
// Assets/Locales/Resources/LocaleSettings.asset so Loc can find it anywhere.
//
// Two kinds of locale, used together:
//   Text locales  - a written language (pt-BR; en, es... later). One strings
//                   file of key -> text.
//   Sign locales  - a sign language written in SignWriting (Libras; ASL...
//                   later). A glosses file of key -> sign(s), plus the sign
//                   library built from its SVGs.
//
// Adding a language is a new folder under Assets/Locales and an entry here -
// see Assets/Locales/README.md.
[CreateAssetMenu(fileName = "LocaleSettings", menuName = "RogueLibras/Locale Settings")]
public class LocaleSettings : ScriptableObject
{
    [System.Serializable]
    public class TextLocale
    {
        [Tooltip("BCP 47 code, e.g. pt-BR, en, es.")]
        public string id;
        public string displayName;
        [Tooltip("strings.json: every text in the game by key.")]
        public TextAsset strings;
    }

    [System.Serializable]
    public class SignLocale
    {
        [Tooltip("e.g. libras, asl.")]
        public string id;
        public string displayName;
        [Tooltip("The text locale its glosses are written in (Libras: pt-BR). " +
                 "Card text is glossed word by word from this language.")]
        public string glossLanguage;
        [Tooltip("glosses.json: which texts are signed, and with which signs.")]
        public TextAsset glosses;
        [Tooltip("The sign sprites, rebuilt from this locale's SVGs.")]
        public SignWritingLibrary library;
    }

    public List<TextLocale> textLocales = new List<TextLocale>();
    public List<SignLocale> signLocales = new List<SignLocale>();

    [Tooltip("The text locale every other one falls back to for missing keys.")]
    public string referenceText = "pt-BR";
    public string defaultText = "pt-BR";
    public string defaultSign = "libras";

    public TextLocale FindText(string id)
    {
        foreach (var l in textLocales) if (l != null && l.id == id) return l;
        return null;
    }

    public SignLocale FindSign(string id)
    {
        foreach (var l in signLocales) if (l != null && l.id == id) return l;
        return null;
    }
}

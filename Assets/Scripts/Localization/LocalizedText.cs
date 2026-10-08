using TMPro;
using UnityEngine;

// Fills a TMP text from the locale files by key (see Loc), so no text is
// typed into scenes or prefabs. When the sign language signs that key (it is
// in its glosses.json) the signs are shown next to the words, so a Libras user
// and a Portuguese reader can both use the menu without either translating.
//
// Labels whose text is decided by code (Recomeçar, a save slot, the mute
// button) do this themselves with Loc.T / Loc.Signed instead.
[RequireComponent(typeof(TMP_Text))]
public class LocalizedText : MonoBehaviour
{
    [Tooltip("Key in Assets/Locales/<language>/strings.json, e.g. menu.play.")]
    [SerializeField] private string key;

    [Tooltip("Show the signs too, when the sign language has them for this key.")]
    [SerializeField] private bool showSigns = true;

    [Tooltip("Inline puts the signs before the words; Above gives them a line " +
             "of their own, for small or squarish buttons.")]
    [SerializeField] private SignText.Layout layout = SignText.Layout.Inline;

    [Tooltip("Size of the signs relative to the text.")]
    [Range(0.6f, 3f)] [SerializeField] private float signScale = 1.3f;

    private TMP_Text _text;

    public string Key
    {
        get { return key; }
        set { key = value; Apply(); }
    }

    private void OnEnable()
    {
        Loc.Changed += Apply;
        Apply();
    }

    private void OnDisable()
    {
        Loc.Changed -= Apply;
    }

    public void Apply()
    {
        if (string.IsNullOrEmpty(key)) return;
        if (_text == null) _text = GetComponent<TMP_Text>();
        SignText.UsePlainSigns(_text);   // thin signs; the card outline is for small card text
        _text.text = showSigns ? Loc.Signed(key, layout, signScale) : Loc.Text(key);
    }

    /// Set up from code - used by the editor tool that wires the scenes.
    public void Configure(string newKey, SignText.Layout newLayout, float newScale, bool signs = true)
    {
        key = newKey;
        layout = newLayout;
        signScale = newScale;
        showSigns = signs;
        Apply();
    }
}

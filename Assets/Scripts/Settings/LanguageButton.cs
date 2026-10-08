using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Switches the menus between Português and Libras (Loc.Display).
//
// In Libras mode every menu text that the sign language fully translates is
// shown as signs instead of words; anything without the whole translation
// stays in Portuguese. The button shows the language in use: the word
// "Português", or the Libras sign.
//
// Installed by MuteButton right next to it, so it appears in every scene
// without anything authored: under Som in battle, to its left in the menu.
[RequireComponent(typeof(RectTransform))]
public class LanguageButton : MonoBehaviour
{
    private const float Gap = 12f;

    private TMP_Text _label;

    /// Places a language button beside the mute button. below: stacked under
    /// it (battle's button column); otherwise to its left (the menu corner).
    public static void InstallNextTo(RectTransform mute, bool below, Color plateColor, TMP_Text fontSource)
    {
        if (mute == null || FindObjectOfType<LanguageButton>(true) != null) return;

        var go = new GameObject("LanguageButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LanguageButton));
        var rt = (RectTransform)go.transform;
        rt.SetParent(mute.parent, false);
        rt.SetSiblingIndex(mute.GetSiblingIndex() + 1);
        rt.anchorMin = mute.anchorMin;
        rt.anchorMax = mute.anchorMax;
        rt.pivot = mute.pivot;
        rt.sizeDelta = mute.sizeDelta;
        rt.anchoredPosition = below
            ? mute.anchoredPosition - new Vector2(0f, mute.rect.height + Gap)
            : mute.anchoredPosition - new Vector2(mute.rect.width + Gap, 0f);

        go.GetComponent<LanguageButton>().Build(plateColor, fontSource);
    }

    private void Build(Color plateColor, TMP_Text fontSource)
    {
        var plate = GetComponent<Image>();
        plate.color = plateColor;

        var button = GetComponent<Button>();
        button.targetGraphic = plate;
        button.onClick.AddListener(Loc.ToggleDisplay);

        var labelGo = new GameObject("Text", typeof(RectTransform));
        labelGo.transform.SetParent(transform, false);
        var lrt = (RectTransform)labelGo.transform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = new Vector2(8f, 4f);
        lrt.offsetMax = new Vector2(-8f, -4f);

        _label = labelGo.AddComponent<TextMeshProUGUI>();
        SignText.UsePlainSigns(_label);
        if (fontSource != null)
        {
            _label.font = fontSource.font;
            _label.color = fontSource.color;
        }
        else _label.color = new Color(0.96f, 0.93f, 0.85f, 1f);
        _label.fontSize = 26f;
        _label.enableAutoSizing = true;
        _label.fontSizeMin = 14f;
        _label.fontSizeMax = 26f;
        _label.enableWordWrapping = false;
        _label.alignment = TextAlignmentOptions.Center;
        _label.raycastTarget = false;

        Refresh();
    }

    private void OnEnable()
    {
        Loc.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        Loc.Changed -= Refresh;
    }

    // The language in use. In Libras mode the Libras sign (the word when the
    // sign is missing); in Português mode the word.
    private void Refresh()
    {
        if (_label == null) return;
        bool signs = Loc.Display == Loc.DisplayMode.Signs;
        string libras = signs ? SignText.AllSignsFor(Loc.Gloss("language.libras"), 2.2f) : null;
        _label.text = libras ?? Loc.T(signs ? "language.libras" : "language.portuguese");
    }
}

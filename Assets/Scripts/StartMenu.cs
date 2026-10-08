using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// What Jogar opens, in two steps:
//
//   1. Continuar - straight into the most recent save - or pick any save from
//      the list, one tab per mode (Português para Libras / Libras para
//      Português). Or start a new game.
//   2. For a new game, which way the words move:
//        Português para Libras - cards start in Portuguese, signs arrive.
//        Libras para Português - cards start in SignWriting (names stay in
//                                Portuguese), Portuguese arrives.
//
// Everything is bilingual - signs and text side by side, from the locale
// files (start.*, direction.*) - and each direction is also shown by example
// (a word turning into its sign, or the other way round), so it reads without
// either language.
//
// Built at runtime over the menu, borrowing the Jogar button's art and font.
public class StartMenu : MonoBehaviour
{
    private static readonly Color BackdropColor = new Color(0f, 0f, 0f, 0.72f);
    private static readonly Color PlateColor = new Color(0.20f, 0.16f, 0.14f, 0.97f);
    private static readonly Color OptionColor = new Color(0.27f, 0.21f, 0.17f, 1f);
    private static readonly Color RowColor = new Color(0.25f, 0.19f, 0.15f, 1f);
    private static readonly Color TabIdleColor = new Color(0.24f, 0.19f, 0.15f, 1f);
    private static readonly Color TabActiveColor = new Color(0.45f, 0.33f, 0.20f, 1f);
    private static readonly Color RimColor = new Color(0.78f, 0.60f, 0.30f, 1f);
    private static readonly Color TextColor = new Color(0.96f, 0.93f, 0.85f, 1f);
    private static readonly Color CaptionColor = new Color(0.86f, 0.80f, 0.68f, 1f);
    private static readonly Color ChipColor = new Color(0.96f, 0.93f, 0.85f, 1f);
    private static readonly Color InkColor = new Color(0.20f, 0.12f, 0.06f, 1f);

    private const float RowHeight = 58f;

    private Action<SignProgression.Direction> _onNewGame;
    private Action<SaveData> _onLoad;
    private TMP_FontAsset _font;
    private Sprite _buttonSprite;
    private RectTransform _page;

    // The save list's tabs and rows, rebuilt when the tab changes.
    private readonly Dictionary<SignProgression.Direction, Image> _tabs = new Dictionary<SignProgression.Direction, Image>();
    private RectTransform _listContent;
    private TMP_Text _listEmpty;

    private static Sprite _arrowSprite;

    /// Opens on the canvas the template button lives on.
    public static StartMenu Show(Button template, Action<SaveData> onLoad,
                                 Action<SignProgression.Direction> onNewGame)
    {
        Canvas canvas = template != null ? template.GetComponentInParent<Canvas>() : null;
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return null;
        canvas = canvas.rootCanvas;

        var go = new GameObject("StartMenu", typeof(RectTransform));
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsLastSibling();

        var menu = go.AddComponent<StartMenu>();
        menu._onLoad = onLoad;
        menu._onNewGame = onNewGame;
        menu.TakeStyleFrom(template);
        menu.BuildBackdrop();
        menu.ShowStartPage();
        return menu;
    }

    private void TakeStyleFrom(Button template)
    {
        if (template == null) return;

        var label = template.GetComponentInChildren<TMP_Text>(true);
        if (label != null) _font = label.font;

        Transform bg = template.transform.Find("ButtonBG");
        Image img = bg != null ? bg.GetComponent<Image>() : template.GetComponent<Image>();
        if (img != null && img.sprite != null) _buttonSprite = img.sprite;
    }

    private void BuildBackdrop()
    {
        Stretch((RectTransform)transform);
        // Full-screen dim that also swallows clicks meant for the menu behind.
        gameObject.AddComponent<Image>().color = BackdropColor;
    }

    private RectTransform NewPage(Vector2 size)
    {
        if (_page != null) Destroy(_page.gameObject);
        _tabs.Clear();
        _page = NewRect("Page", transform);
        _page.sizeDelta = size;
        _page.gameObject.AddComponent<Image>().color = PlateColor;
        var rim = _page.gameObject.AddComponent<Outline>();
        rim.effectColor = RimColor;
        rim.effectDistance = new Vector2(4f, -4f);
        return _page;
    }

    // ---- page 1: continue, any save, or a new game ---------------------------------

    private void ShowStartPage()
    {
        RectTransform page = NewPage(new Vector2(1300f, 980f));

        Label(page, Loc.Signed("start.title", SignText.Layout.Above, 1.5f), 50f, TextColor,
              new Vector2(0f, 420f), new Vector2(1200f, 120f));

        // Quick access: the newest save, whichever mode it is in.
        SaveData latest = SaveSystem.LoadMostRecent();
        Button cont = OrnateButton(page, Loc.Signed("start.continue"), new Vector2(0f, 275f), new Vector2(564f, 171f));
        if (latest != null) cont.onClick.AddListener(() => Load(latest));
        else
        {
            cont.interactable = false;
            cont.GetComponent<CanvasGroup>().alpha = 0.45f;
        }
        Label(page, latest != null ? latest.Describe() : Loc.Text("start.continue_none"), 24f, CaptionColor,
              new Vector2(0f, 165f), new Vector2(1200f, 44f));

        // Every save, one tab per mode.
        Label(page, Loc.Signed("start.saves"), 30f, TextColor, new Vector2(0f, 122f), new Vector2(1200f, 44f));
        Tab(page, SignProgression.Direction.PortugueseToLibras, new Vector2(-300f, 42f));
        Tab(page, SignProgression.Direction.LibrasToPortuguese, new Vector2(300f, 42f));
        BuildList(page, new Vector2(0f, -182f), new Vector2(1180f, 300f));
        ShowSaves(latest != null ? latest.Direction : SignProgression.CurrentDirection);

        Button newGame = OrnateButton(page, Loc.Signed("start.new_game"), new Vector2(-170f, -405f), new Vector2(500f, 152f));
        newGame.onClick.AddListener(ShowDirectionPage);

        Button back = PlateButton(page, Loc.Signed("start.back"), new Vector2(300f, -405f), new Vector2(260f, 72f));
        back.onClick.AddListener(Close);
    }

    private void Tab(RectTransform page, SignProgression.Direction direction, Vector2 at)
    {
        RectTransform rt = NewRect("Tab", page);
        rt.anchoredPosition = at;
        rt.sizeDelta = new Vector2(580f, 104f);
        var img = rt.gameObject.AddComponent<Image>();
        var rim = rt.gameObject.AddComponent<Outline>();
        rim.effectColor = RimColor;
        rim.effectDistance = new Vector2(2f, -2f);

        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        Tint(button);
        button.onClick.AddListener(() => ShowSaves(direction));

        // The signs are the label - [Português] -> [Libras], the arrow saying
        // "para" - and the Portuguese sits under them as a caption.
        bool toSigns = direction == SignProgression.Direction.PortugueseToLibras;
        RectTransform signs = Row(rt, new Vector2(0f, 14f), 62f, 18f);
        SignsOrWord(signs, toSigns ? "language.portuguese" : "language.libras", 50f);
        Arrow(signs, TextColor);
        SignsOrWord(signs, toSigns ? "language.libras" : "language.portuguese", 50f);

        TMP_Text caption = Label(rt, Loc.T(SignProgression.DescribeKey(direction)), 22f, CaptionColor,
                                 new Vector2(0f, -33f), new Vector2(560f, 30f));
        caption.enableAutoSizing = true;
        caption.fontSizeMin = 14f;
        caption.fontSizeMax = 22f;

        _tabs[direction] = img;
    }

    // A scrolling list: ten slots at most, five or so in view.
    private void BuildList(RectTransform page, Vector2 at, Vector2 size)
    {
        RectTransform view = NewRect("Saves", page);
        view.anchoredPosition = at;
        view.sizeDelta = size;
        view.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.25f);
        view.gameObject.AddComponent<RectMask2D>();

        _listContent = NewRect("Content", view);
        _listContent.anchorMin = new Vector2(0f, 1f);
        _listContent.anchorMax = new Vector2(1f, 1f);
        _listContent.pivot = new Vector2(0.5f, 1f);
        _listContent.offsetMin = _listContent.offsetMax = Vector2.zero;
        var layout = _listContent.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        _listContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var scroll = view.gameObject.AddComponent<ScrollRect>();
        scroll.content = _listContent;
        scroll.viewport = view;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;

        _listEmpty = Label(view, Loc.Text("start.saves_empty"), 26f, CaptionColor, Vector2.zero, new Vector2(size.x - 40f, 60f));
    }

    private void ShowSaves(SignProgression.Direction direction)
    {
        foreach (var kv in _tabs) kv.Value.color = kv.Key == direction ? TabActiveColor : TabIdleColor;

        for (int i = _listContent.childCount - 1; i >= 0; i--) Destroy(_listContent.GetChild(i).gameObject);

        var saves = new List<SaveData>();
        for (int slot = 0; slot < SaveSystem.SlotCount; slot++)
        {
            SaveData data = SaveSystem.Load(slot);
            if (data != null && data.Direction == direction) saves.Add(data);
        }
        saves.Sort((a, b) => b.SavedAt.CompareTo(a.SavedAt));   // newest first

        foreach (var save in saves) SaveRow(save);
        _listEmpty.gameObject.SetActive(saves.Count == 0);
        _listContent.anchoredPosition = Vector2.zero;
    }

    private void SaveRow(SaveData save)
    {
        RectTransform row = NewRect("Save" + save.slot, _listContent);
        row.gameObject.AddComponent<Image>().color = RowColor;
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = RowHeight;

        TMP_Text t = Label(row, Loc.T("settings.slot", save.slot + 1) + "   <color=#DCCCAD>" + save.Describe(false) + "</color>",
                           22f, TextColor, Vector2.zero, Vector2.zero);
        t.alignment = TextAlignmentOptions.MidlineLeft;
        Stretch(t.rectTransform);
        t.rectTransform.offsetMin = new Vector2(18f, 0f);
        t.rectTransform.offsetMax = new Vector2(-250f, 0f);
        t.enableAutoSizing = true;
        t.fontSizeMin = 14f;
        t.fontSizeMax = 22f;

        Button load = PlateButton(row, Loc.Signed("start.load", SignText.Layout.Inline, 1.1f), Vector2.zero, new Vector2(220f, 46f));
        var lrt = (RectTransform)load.transform;
        lrt.anchorMin = lrt.anchorMax = new Vector2(1f, 0.5f);
        lrt.pivot = new Vector2(1f, 0.5f);
        lrt.anchoredPosition = new Vector2(-12f, 0f);
        load.onClick.AddListener(() => Load(save));
    }

    // ---- page 2: which way the words move ---------------------------------------

    private void ShowDirectionPage()
    {
        RectTransform page = NewPage(new Vector2(1560f, 900f));

        Label(page, Loc.Signed("start.new_game", SignText.Layout.Inline, 1.4f), 50f, TextColor,
              new Vector2(0f, 375f), new Vector2(1400f, 90f));
        Label(page, Loc.Text("start.choose"), 28f, CaptionColor, new Vector2(0f, 310f), new Vector2(1400f, 50f));

        bool lastWasLibras = SignProgression.CurrentDirection == SignProgression.Direction.LibrasToPortuguese;
        Option(page, new Vector2(-370f, -15f), SignProgression.Direction.PortugueseToLibras, !lastWasLibras);
        Option(page, new Vector2(370f, -15f), SignProgression.Direction.LibrasToPortuguese, lastWasLibras);

        Button back = PlateButton(page, Loc.Signed("start.back"), new Vector2(0f, -385f), new Vector2(260f, 72f));
        back.onClick.AddListener(ShowStartPage);
    }

    private void Option(RectTransform page, Vector2 at, SignProgression.Direction direction, bool wasLast)
    {
        bool toSigns = direction == SignProgression.Direction.PortugueseToLibras;

        RectTransform rt = NewRect(direction.ToString(), page);
        rt.anchoredPosition = at;
        rt.sizeDelta = new Vector2(690f, 560f);
        var plate = rt.gameObject.AddComponent<Image>();
        plate.color = OptionColor;
        var rim = rt.gameObject.AddComponent<Outline>();
        rim.effectColor = wasLast ? RimColor : new Color(RimColor.r, RimColor.g, RimColor.b, 0.35f);
        rim.effectDistance = new Vector2(3f, -3f);

        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = plate;
        Tint(button);
        button.onClick.AddListener(() => Pick(direction));

        // Signs first: [Português] -> [Libras], or the other way round.
        string from = toSigns ? "language.portuguese" : "language.libras";
        string to = toSigns ? "language.libras" : "language.portuguese";
        RectTransform header = Row(rt, new Vector2(0f, 200f), 110f, 24f);
        SignsOrWord(header, from);
        Arrow(header, TextColor);
        SignsOrWord(header, to);

        string key = SignProgression.DescribeKey(direction);
        Label(rt, Loc.T(key), 38f, TextColor, new Vector2(0f, 115f), new Vector2(640f, 56f));

        // The same thing by example: a word becoming its sign, or back.
        RectTransform example = Row(rt, new Vector2(0f, -25f), 200f, 28f);
        if (toSigns) { WordChip(example); Arrow(example, RimColor); SignChip(example); }
        else { SignChip(example); Arrow(example, RimColor); WordChip(example); }

        Label(rt, Loc.Text(key + "_help"), 25f, CaptionColor, new Vector2(0f, -185f), new Vector2(640f, 90f));

        if (wasLast)
            Label(rt, "<i>" + Loc.Text("start.previous_choice") + "</i>", 21f, RimColor,
                  new Vector2(0f, -252f), new Vector2(640f, 36f));
    }

    // ---- actions ---------------------------------------------------------------------

    private void Load(SaveData save)
    {
        var onLoad = _onLoad;
        Close();
        if (onLoad != null) onLoad(save);
    }

    private void Pick(SignProgression.Direction direction)
    {
        var onNewGame = _onNewGame;
        Close();
        if (onNewGame != null) onNewGame(direction);
    }

    public void Close()
    {
        _onLoad = null;
        _onNewGame = null;
        Destroy(gameObject);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    // ---- building blocks -------------------------------------------------------------

    private Button OrnateButton(RectTransform parent, string label, Vector2 at, Vector2 size)
    {
        RectTransform rt = NewRect("Button", parent);
        rt.anchoredPosition = at;
        rt.sizeDelta = size;   // the menu button art is 564 x 171
        rt.gameObject.AddComponent<CanvasGroup>();

        var img = rt.gameObject.AddComponent<Image>();
        if (_buttonSprite != null) img.sprite = _buttonSprite;
        else img.color = new Color(0.36f, 0.27f, 0.20f, 1f);

        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        Tint(button);

        // Clear of the ornaments at either end, which scale with the button.
        float k = size.x / 564f;
        TMP_Text t = Label(rt, label, 44f, TextColor, Vector2.zero, Vector2.zero);
        Stretch(t.rectTransform);
        t.rectTransform.offsetMin = new Vector2(112f * k, 22f * k);
        t.rectTransform.offsetMax = new Vector2(-112f * k, -22f * k);
        t.enableWordWrapping = false;   // signs and words on one line; shrink to fit instead
        t.enableAutoSizing = true;
        t.fontSizeMin = 20f;
        t.fontSizeMax = 44f;
        return button;
    }

    private Button PlateButton(RectTransform parent, string label, Vector2 at, Vector2 size)
    {
        RectTransform rt = NewRect("Button", parent);
        rt.anchoredPosition = at;
        rt.sizeDelta = size;

        var img = rt.gameObject.AddComponent<Image>();
        img.color = new Color(0.32f, 0.25f, 0.20f, 1f);
        var rim = rt.gameObject.AddComponent<Outline>();
        rim.effectColor = RimColor;
        rim.effectDistance = new Vector2(2f, -2f);

        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = img;
        Tint(button);

        TMP_Text t = Label(rt, label, 30f, TextColor, Vector2.zero, Vector2.zero);
        Stretch(t.rectTransform);
        t.rectTransform.offsetMin = new Vector2(12f, 6f);
        t.rectTransform.offsetMax = new Vector2(-12f, -6f);
        t.enableAutoSizing = true;
        t.fontSizeMin = 16f;
        t.fontSizeMax = 30f;
        return button;
    }

    // A centred row that sizes itself to its children.
    private RectTransform Row(RectTransform parent, Vector2 at, float height, float spacing)
    {
        RectTransform rt = NewRect("Row", parent);
        rt.anchoredPosition = at;
        rt.sizeDelta = new Vector2(640f, height);
        var row = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        row.childAlignment = TextAnchor.MiddleCenter;
        row.spacing = spacing;
        row.childControlWidth = true;
        row.childControlHeight = true;
        row.childForceExpandWidth = false;
        row.childForceExpandHeight = false;
        return rt;
    }

    // A language name as its signs, or written when it has none.
    private void SignsOrWord(RectTransform row, string key, float signSize = 64f)
    {
        string signs = SignText.SignsFor(Loc.Gloss(key), 1f);
        Label(row, signs ?? Loc.T(key), signs != null ? signSize : signSize * 0.6f, TextColor, Vector2.zero, Vector2.zero);
    }

    private void WordChip(RectTransform row)
    {
        RectTransform chip = Chip(row);
        TMP_Text t = Label(chip, Loc.T("start.example_word"), 44f, InkColor, Vector2.zero, Vector2.zero);
        Stretch(t.rectTransform);
    }

    // The same sign art the card itself shows (the library sprite, in its own
    // colour), not the inline atlas the menus would use.
    private void SignChip(RectTransform row)
    {
        RectTransform chip = Chip(row);
        string word = Loc.T("start.example_word");
        SignWritingLibrary library = Loc.Signs;
        Sprite[] signs = library != null ? library.Get(word) : null;
        if (signs == null || signs.Length == 0)
        {
            TMP_Text t = Label(chip, word, 40f, InkColor, Vector2.zero, Vector2.zero);
            Stretch(t.rectTransform);
            return;
        }

        RectTransform art = NewRect("Signs", chip);
        Stretch(art);
        art.offsetMin = new Vector2(16f, 22f);
        art.offsetMax = new Vector2(-16f, -22f);
        var layout = art.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 4f;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = layout.childForceExpandHeight = true;
        foreach (var sprite in signs)
        {
            var img = NewRect("Sign", art).gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
        }
    }

    // Card-shaped: the example is a card's name, written or signed.
    private RectTransform Chip(RectTransform row)
    {
        RectTransform rt = NewRect("Card", row);
        rt.gameObject.AddComponent<Image>().color = ChipColor;
        var rim = rt.gameObject.AddComponent<Outline>();
        rim.effectColor = RimColor;
        rim.effectDistance = new Vector2(3f, -3f);
        var le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = 150f;
        le.preferredHeight = 200f;
        return rt;
    }

    private void Arrow(RectTransform row, Color color)
    {
        RectTransform rt = NewRect("Arrow", row);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = ArrowSprite();
        img.color = color;
        img.raycastTarget = false;
        var le = rt.gameObject.AddComponent<LayoutElement>();
        le.preferredWidth = 72f;
        le.preferredHeight = 36f;
    }

    private static void Tint(Button button)
    {
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        colors.highlightedColor = Color.white;
        colors.selectedColor = Color.white;
        colors.pressedColor = new Color(0.75f, 0.75f, 0.75f, 1f);
        colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 1f);
        button.colors = colors;
    }

    private TMP_Text Label(RectTransform parent, string text, float size, Color color, Vector2 at, Vector2 box)
    {
        RectTransform rt = NewRect("Text", parent);
        rt.anchoredPosition = at;
        rt.sizeDelta = box;

        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        SignText.UsePlainSigns(t);
        if (_font != null) t.font = _font;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.raycastTarget = false;
        return t;
    }

    private static RectTransform NewRect(string objectName, Transform parent)
    {
        var go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        return rt;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // A plain right-pointing arrow: a shaft and a head.
    private static Sprite ArrowSprite()
    {
        if (_arrowSprite != null) return _arrowSprite;

        const int w = 64, h = 32;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w, v = (y + 0.5f) / h, dy = Mathf.Abs(v - 0.5f);
                bool shaft = u < 0.62f && dy < 0.12f;
                bool head = u >= 0.55f && dy < (1f - u) / 0.45f * 0.5f;
                px[y * w + x] = new Color32(255, 255, 255, (byte)(shaft || head ? 255 : 0));
            }
        tex.SetPixels32(px);
        tex.Apply();
        _arrowSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        return _arrowSprite;
    }
}

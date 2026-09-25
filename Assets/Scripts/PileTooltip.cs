using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Hover tooltip for the draw and discard piles: what the pile is and how many
// cards are in it, in the same look as the Ajuda button.
//
// The pile's name goes through the same text -> sign staging as the cards:
// while SignProgression still has the word as text it is written out, and once
// it has graduated its SignWriting is shown instead, on a light tile because the
// signs are dark line art. Nothing teaches "compra" or "descarte" yet - add them
// to the words CardManager.AdvanceSignProgression offers when they should start
// graduating, and the tooltip follows on its own.
[DisallowMultipleComponent]
public class PileTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public enum Pile { Draw, Discard }

    [SerializeField] private Pile pile;
    [Tooltip("The pile's name - also the SignWriting word that replaces it.")]
    [SerializeField] private string word = "Compra";
    [Tooltip("Second line. {0} is the number of cards in the pile.")]
    [SerializeField] private string countFormat = "{0} cartas para comprar";
    [Tooltip("Second line when the pile holds exactly one card.")]
    [SerializeField] private string singularFormat = "1 carta para comprar";
    [SerializeField] private SignWritingLibrary signLibrary;

    [Header("Text")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Material fontMaterial;

    [Header("Colours")]
    [SerializeField] private Color plateColor = new Color(0.20f, 0.16f, 0.14f, 1f);
    [SerializeField] private Color rimColor = new Color(0.78f, 0.60f, 0.30f, 1f);
    [Tooltip("Title. About 13:1 on the plate.")]
    [SerializeField] private Color textColor = new Color(0.96f, 0.93f, 0.85f, 1f);
    [Tooltip("Count line. About 9:1 on the plate.")]
    [SerializeField] private Color hintColor = new Color(0.86f, 0.78f, 0.64f, 1f);
    [Tooltip("Behind a sign, which is dark line art.")]
    [SerializeField] private Color signTileColor = new Color(0.98f, 0.955f, 0.90f, 1f);

    [Header("Behaviour")]
    [Tooltip("Seconds the pointer must rest on the pile first, so sweeping past it doesn't flash the tooltip.")]
    [SerializeField] private float hoverDelay = 0.25f;
    [SerializeField] private float signHeight = 64f;
    [SerializeField] private float gap = 12f;

    private RectTransform _root;
    private RectTransform _tip;
    private CanvasGroup _group;
    private TMP_Text _title;
    private RectTransform _signTile;
    private RectTransform _signRow;
    private readonly List<Image> _signs = new List<Image>();
    private TMP_Text _count;

    private bool _hovered;
    private float _hoverTime;
    private float _show;

    public void OnPointerEnter(PointerEventData eventData) { _hovered = true; _hoverTime = 0f; }
    public void OnPointerExit(PointerEventData eventData)  { _hovered = false; }

    private void OnDisable()
    {
        _hovered = false;
        _show = 0f;
        if (_tip != null) _tip.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (_tip != null) Destroy(_tip.gameObject);
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (_hovered) _hoverTime += dt;

        bool want = _hovered && _hoverTime >= hoverDelay;
        _show = Mathf.MoveTowards(_show, want ? 1f : 0f, dt / 0.15f);
        if (_show <= 0f && _tip == null) return;

        Build();
        _tip.gameObject.SetActive(_show > 0f);
        if (_show <= 0f) return;

        if (want) Refresh();

        _group.alpha = _show;
        _tip.SetAsLastSibling();
        Place(1f - _show);
    }

    private void Refresh()
    {
        Sprite[] signs = signLibrary != null ? signLibrary.Get(word) : null;
        bool asSign = signs != null && signs.Length > 0 && SignProgression.GetStage(word) == SignProgression.Stage.Sign;

        _title.gameObject.SetActive(!asSign);
        _signTile.gameObject.SetActive(asSign);
        if (asSign) FillSigns(signs);
        else _title.text = word;

        int count = CardCount();
        _count.text = count == 1 && !string.IsNullOrEmpty(singularFormat)
            ? singularFormat
            : string.Format(countFormat, count);
        LayoutRebuilder.ForceRebuildLayoutImmediate(_tip);
    }

    private int CardCount()
    {
        CardManager cm = CardManager.instance;
        if (cm == null) return 0;
        Transform container = pile == Pile.Draw ? cm.drawContainer : cm.discardContainer;
        return container != null ? container.childCount : 0;
    }

    // Above the pile, centred on it, kept on screen. Rises slightly as it fades in.
    private void Place(float rise)
    {
        var self = (RectTransform)transform;
        var corners = new Vector3[4];
        self.GetWorldCorners(corners);

        Vector2 min = _root.InverseTransformPoint(corners[0]);
        Vector2 max = _root.InverseTransformPoint(corners[2]);
        Rect area = _root.rect;
        Vector2 size = _tip.rect.size;

        float x = Mathf.Clamp((min.x + max.x) * 0.5f, area.xMin + gap + size.x * 0.5f, area.xMax - gap - size.x * 0.5f);
        float y = Mathf.Min(max.y + gap, area.yMax - gap - size.y) - rise * 10f;
        _tip.anchoredPosition = new Vector2(x, y);
    }

    private void FillSigns(Sprite[] signs)
    {
        while (_signs.Count < signs.Length)
        {
            var img = NewRect("Sign", _signRow).gameObject.AddComponent<Image>();
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.gameObject.AddComponent<LayoutElement>();
            _signs.Add(img);
        }

        for (int i = 0; i < _signs.Count; i++)
        {
            bool used = i < signs.Length;
            _signs[i].gameObject.SetActive(used);
            if (!used) continue;

            Sprite s = signs[i];
            _signs[i].sprite = s;
            float aspect = s != null && s.rect.height > 0f ? s.rect.width / s.rect.height : 1f;
            var le = _signs[i].GetComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = signHeight * aspect;
            le.preferredHeight = le.minHeight = signHeight;
        }
    }

    // ---- building ------------------------------------------------------------

    private void Build()
    {
        if (_tip != null) return;

        var canvas = GetComponentInParent<Canvas>();
        _root = (RectTransform)(canvas != null ? canvas.rootCanvas.transform : transform.root);
        if (font == null) font = TMP_Settings.defaultFontAsset;

        _tip = NewRect(name + " Tooltip", _root);
        _tip.pivot = new Vector2(0.5f, 0f);
        _group = _tip.gameObject.AddComponent<CanvasGroup>();
        _group.blocksRaycasts = false;   // never steals the hover from the pile

        Plate(_tip, plateColor, rimColor);

        var layout = _tip.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 12, 14);
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fit = _tip.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _title = NewText("Title", _tip, word, 26f, textColor);
        _title.fontStyle = FontStyles.Bold;
        _title.gameObject.AddComponent<LayoutElement>().minWidth = 160f;

        _signTile = NewRect("SignTile", _tip);
        Plate(_signTile, signTileColor, rimColor);
        var tl = _signTile.gameObject.AddComponent<HorizontalLayoutGroup>();
        tl.padding = new RectOffset(12, 12, 8, 8);
        tl.childAlignment = TextAnchor.MiddleCenter;
        tl.childControlWidth = tl.childControlHeight = true;
        tl.childForceExpandWidth = tl.childForceExpandHeight = false;
        _signRow = NewRect("Signs", _signTile);
        var rl = _signRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        rl.spacing = 8f;
        rl.childAlignment = TextAnchor.MiddleCenter;
        rl.childControlWidth = rl.childControlHeight = true;
        rl.childForceExpandWidth = rl.childForceExpandHeight = false;
        _signTile.gameObject.SetActive(false);

        _count = NewText("Count", _tip, "", 19f, hintColor);

        _tip.gameObject.SetActive(false);
    }

    private static void Plate(RectTransform rt, Color fill, Color rim)
    {
        var plate = rt.gameObject.AddComponent<Image>();
        plate.sprite = CardHelp.PlateSprite();
        plate.type = Image.Type.Sliced;
        plate.color = fill;
        plate.raycastTarget = false;

        var border = NewRect("Rim", rt).gameObject.AddComponent<Image>();
        border.sprite = CardHelp.RingSprite();
        border.type = Image.Type.Sliced;
        border.color = rim;
        border.raycastTarget = false;
        border.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        var brt = border.rectTransform;
        brt.anchorMin = Vector2.zero;
        brt.anchorMax = Vector2.one;
        brt.offsetMin = brt.offsetMax = Vector2.zero;
    }

    private TMP_Text NewText(string objectName, Transform parent, string text, float size, Color color)
    {
        var t = NewRect(objectName, parent).gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        if (fontMaterial != null) t.fontSharedMaterial = fontMaterial;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = false;
        t.raycastTarget = false;
        return t;
    }

    private static RectTransform NewRect(string objectName, Transform parent)
    {
        var go = new GameObject(objectName, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        return rt;
    }
}

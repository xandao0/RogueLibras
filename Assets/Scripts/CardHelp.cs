using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Ajuda: lets the player look closely at any word or sign on a card, and see it
// in the other language.
//
// Selecting a card slides an "Ajuda" button out beside it. Pressing it turns on
// help mode: every word and sign on the card gets an outline, and clicking one
// opens a word card showing it large enough to read, with a button that reveals
// it in the other language - the Portuguese for a sign, the sign for a
// Portuguese word - the way Duolingo reveals a word's meaning on tap. Clicks on
// the card never play it while help is open. Clicking outside the card, pressing
// Ajuda again, or Esc leaves help mode.
//
// Colours follow WCAG 2.1 AA: the word card is dark ink on an opaque light panel
// and the prompt is cream on the button's dark plate, 7:1 or better either way.
// The words glow rather than sit in boxes, and the glow fades out completely
// between pulses so it never stays over the text being read.
//
// Sits on the hand's canvas and builds its own UI at runtime, the same way the
// card shine and the burn effect build theirs.
[DisallowMultipleComponent]
public class CardHelp : MonoBehaviour
{
    public static CardHelp instance { get; private set; }

    [Header("Text")]
    [SerializeField] private TMP_FontAsset font;
    [Tooltip("Optional. The font's own material when left empty.")]
    [SerializeField] private Material fontMaterial;

    [Header("Ajuda button")]
    [SerializeField] private Color plateColor = new Color(0.20f, 0.16f, 0.14f, 1f);
    [SerializeField] private Color rimColor = new Color(0.78f, 0.60f, 0.30f, 1f);
    [SerializeField] private Color textColor = new Color(0.96f, 0.93f, 0.85f, 1f);
    [SerializeField] private Color accentColor = new Color(1f, 0.78f, 0.35f, 1f);
    [Tooltip("Second line of the prompt. About 9:1 on the button plate.")]
    [SerializeField] private Color promptHintColor = new Color(0.86f, 0.78f, 0.64f, 1f);

    [Header("Panels (prompt and word card)")]
    [SerializeField] private Color panelColor = new Color32(0xFB, 0xF4, 0xE6, 0xFF);
    [SerializeField] private Color panelBorderColor = new Color32(0x3B, 0x26, 0x16, 0xFF);
    [Tooltip("Main text. 16:1 on the panel.")]
    [SerializeField] private Color inkColor = new Color32(0x1E, 0x13, 0x0B, 0xFF);
    [Tooltip("Secondary text. 9.7:1 on the panel.")]
    [SerializeField] private Color inkMutedColor = new Color32(0x4E, 0x3B, 0x2C, 0xFF);
    [Tooltip("Small headings. 7.6:1 on the panel, 6.6:1 on the tile.")]
    [SerializeField] private Color headingColor = new Color32(0x8C, 0x2F, 0x0C, 0xFF);
    [Tooltip("Background of the revealed translation.")]
    [SerializeField] private Color tileColor = new Color32(0xF1, 0xE3, 0xC8, 0xFF);
    [SerializeField] private Color actionColor = new Color32(0x3B, 0x26, 0x16, 0xFF);
    [SerializeField] private Color actionHoverColor = new Color32(0x6B, 0x3F, 0x1E, 0xFF);
    [Tooltip("Button text. 13:1 on the button.")]
    [SerializeField] private Color actionTextColor = new Color32(0xFF, 0xF6, 0xE5, 0xFF);

    [Header("Outlines")]
    [SerializeField] private Color focusColor = new Color32(0xFF, 0xC9, 0x28, 0xFF);

    [Header("Layout")]
    [SerializeField] private Vector2 buttonSize = new Vector2(164f, 58f);
    [Tooltip("Gap between the card's edge and the button.")]
    [SerializeField] private float buttonGap = 16f;
    [SerializeField] private float screenMargin = 14f;
    [SerializeField] private float promptWidth = 340f;
    [SerializeField] private float wordCardMinWidth = 340f;
    [SerializeField] private float bigSignHeight = 120f;
    [SerializeField] private float translationSignHeight = 90f;

    [Header("Timing")]
    [SerializeField] private float showDuration = 0.4f;
    [SerializeField] private float hideDuration = 0.14f;

    private Canvas _canvas;
    private RectTransform _root;

    private RectTransform _button;
    private CanvasGroup _buttonGroup;
    private Image _buttonPlate;
    private Image _buttonRim;
    private Image _buttonShine;
    private RectTransform _icon;

    private RectTransform _prompt;
    private CanvasGroup _promptGroup;

    // The word card: the picked word or sign, large, plus its translation.
    private RectTransform _word;
    private CanvasGroup _wordGroup;
    private TMP_Text _wordHeading;
    private TMP_Text _wordBigText;
    private RectTransform _wordBigSigns;
    private TMP_Text _wordRole;
    private RectTransform _trans;
    private CanvasGroup _transGroup;
    private TMP_Text _transHeading;
    private TMP_Text _transText;
    private RectTransform _transSigns;
    private TMP_Text _noSign;
    private Button _translateButton;
    private readonly List<Image> _bigSignPool = new List<Image>();
    private readonly List<Image> _transSignPool = new List<Image>();

    private RectTransform _highlights;
    private readonly List<Image> _rings = new List<Image>();

    private CardDisplay _card;          // the card the button belongs to
    private bool _helpMode;
    private float _show;                // button: 0 hidden .. 1 shown
    private float _showTime;            // seconds since the button started appearing
    private Vector2 _buttonPos;
    private bool _onLeft;
    private float _promptShow;
    private float _promptShake;
    private float _helpTime;

    private bool _hasPick;
    private CardDisplay.WordSpot _pick;
    private bool _pickIsSign;
    private bool _translated;
    private float _wordTime;
    private float _transTime;

    private readonly List<CardDisplay.WordSpot> _spots = new List<CardDisplay.WordSpot>();
    private readonly List<Rect> _spotRects = new List<Rect>();
    private readonly Vector3[] _corners = new Vector3[4];

    private static Sprite _plateSprite, _ringSprite, _circleSprite, _bandSprite;
    private Sprite _focusSprite;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(this);
            return;
        }
        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    /// Called by a card before it handles a click. In help mode a click on the
    /// card picks a word, and never plays the card; a click on any other card
    /// just leaves help mode.
    public bool HandleCardClick(CardDisplay card, PointerEventData eventData)
    {
        if (!_helpMode || card == null) return false;

        if (card != _card)
        {
            SetHelpMode(false);
            return true;
        }

        int hit = HitTest(eventData.position, eventData.pressEventCamera);
        if (hit >= 0)
        {
            OpenWordCard(_spots[hit]);
        }
        else
        {
            _hasPick = false;    // off the words: put the word card away
            _promptShake = 1f;   // and nudge the prompt - "pick a word"
        }
        return true;
    }

    /// A click that landed off the card and off every button. Leaves help mode
    /// and reports whether it did, so the click doesn't also put the card down.
    public bool CloseFromOutsideClick()
    {
        if (!_helpMode) return false;
        SetHelpMode(false);
        return true;
    }

    // ---- per frame ---------------------------------------------------------

    private void LateUpdate()
    {
        CardDisplay target = CurrentTarget();
        if (target != _card)
        {
            _card = target;
            SetHelpMode(false);
            if (_card != null) { _showTime = 0f; _show = 0f; }
        }

        if (_card == null && _show <= 0f && _root == null) return;
        Build();

        float dt = Time.unscaledDeltaTime;
        if (_helpMode && Input.GetKeyDown(KeyCode.Escape))
        {
            if (_hasPick) _hasPick = false;
            else SetHelpMode(false);
        }

        if (_card != null)
        {
            _showTime += dt;
            _show = Mathf.Clamp01(_showTime / Mathf.Max(0.01f, showDuration));
            _root.SetAsLastSibling();   // above the hand, which reorders itself
            PlaceButton();
        }
        else
        {
            _show = Mathf.MoveTowards(_show, 0f, dt / Mathf.Max(0.01f, hideDuration));
        }

        AnimateButton();

        _promptShow = Mathf.MoveTowards(_promptShow, _helpMode ? 1f : 0f, dt * 6f);
        _promptShake = Mathf.MoveTowards(_promptShake, 0f, dt * 3f);
        if (_helpMode) _helpTime += dt;
        AnimatePrompt();

        UpdateSpots();
        AnimateHighlights();
        AnimateWordCard(dt);
    }

    private CardDisplay CurrentTarget()
    {
        CardManager cm = CardManager.instance;
        if (cm == null || cm.currentTurn != CurrentTurn.PLAYERTURN) return null;
        if (GameManager.instance != null && GameManager.instance.currentState != GameStates.COMBAT) return null;

        CardDisplay selected = cm.selectedCard;
        if (selected == null || selected.transform.parent != cm.cardHolderContainer) return null;
        return selected;
    }

    private void ToggleHelp()
    {
        if (_card != null) SetHelpMode(!_helpMode);
    }

    private void SetHelpMode(bool on)
    {
        _helpMode = on;
        _hasPick = false;
        _helpTime = 0f;

        // The description's glyph positions are read back to find the words.
        if (on && _card != null && _card.cardDescriptionText != null)
            _card.cardDescriptionText.ForceMeshUpdate();
    }

    // Beside the card's top corner: right if there's room, otherwise left.
    private void PlaceButton()
    {
        Rect card = RootRect((RectTransform)_card.transform);
        Rect area = _root.rect;
        Vector2 half = buttonSize * 0.5f;

        float right = card.xMax + buttonGap + half.x;
        float left = card.xMin - buttonGap - half.x;
        bool fitsRight = right + half.x <= area.xMax - screenMargin;
        bool fitsLeft = left - half.x >= area.xMin + screenMargin;
        _onLeft = !fitsRight && fitsLeft;

        float x = _onLeft ? left : Mathf.Min(right, area.xMax - screenMargin - half.x);
        float y = card.yMax - half.y - 8f;
        y = Mathf.Clamp(y, area.yMin + screenMargin + half.y, area.yMax - screenMargin - half.y);
        _buttonPos = new Vector2(x, y);
    }

    // The entrance: slides out from behind the card, overshoots a little, then a
    // glint runs across it. Hover brightens it; help mode lights the rim.
    private void AnimateButton()
    {
        bool showing = _card != null;
        float p = _show;

        float alpha, scale, slide;
        if (showing)
        {
            alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(p / 0.6f));
            scale = Mathf.LerpUnclamped(0.6f, 1f, EaseOutBack(p));
            slide = (1f - EaseOutCubic(p)) * 40f;
        }
        else
        {
            alpha = p;
            scale = Mathf.Lerp(0.85f, 1f, p);
            slide = 0f;
        }

        _buttonGroup.alpha = alpha;
        _buttonGroup.interactable = _buttonGroup.blocksRaycasts = showing && p > 0.5f;
        _button.gameObject.SetActive(alpha > 0.001f);
        _button.anchoredPosition = _buttonPos + new Vector2(_onLeft ? slide : -slide, 0f);

        bool hovered = showing && RectTransformUtility.RectangleContainsScreenPoint(_button, Input.mousePosition, EventCamera());
        _button.localScale = Vector3.one * scale * (hovered ? 1.05f : 1f);
        _buttonPlate.color = hovered ? Color.Lerp(plateColor, rimColor, 0.25f) : plateColor;

        float pulse = 0.5f + 0.5f * Mathf.Sin(_helpTime * 5f);
        _buttonRim.color = _helpMode ? Color.Lerp(accentColor, Color.white, 0.35f * pulse) : rimColor;

        // A glint crosses the button once it has settled.
        float glintT = (_showTime - showDuration * 0.55f) / 0.55f;
        bool glint = showing && glintT > 0f && glintT < 1f;
        _buttonShine.gameObject.SetActive(glint);
        if (glint)
        {
            float w = buttonSize.x;
            _buttonShine.rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-w, w, glintT), 0f);
            _buttonShine.color = new Color(1f, 0.96f, 0.85f, 0.7f * Mathf.Sin(glintT * Mathf.PI));
        }

        // The "?" bobs while help mode waits for a pick.
        float bob = _helpMode && !_hasPick ? Mathf.Sin(_helpTime * 6f) * 0.08f : 0f;
        _icon.localScale = Vector3.one * (1f + bob);
    }

    private void AnimatePrompt()
    {
        _prompt.gameObject.SetActive(_promptShow > 0.001f);
        if (!_prompt.gameObject.activeSelf) return;

        float e = EaseOutCubic(_promptShow);
        _promptGroup.alpha = e;

        float shake = Mathf.Sin(_promptShake * 40f) * 8f * _promptShake;
        float y = _button.anchoredPosition.y - buttonSize.y * 0.5f - 10f - (1f - e) * 12f;

        // Under the button, starting at the card's edge and growing away from
        // it - never over the card. Same side as the button when it fits there.
        Rect card = _card != null ? RootRect((RectTransform)_card.transform) : _lastCardRect;
        if (_card != null) _lastCardRect = card;
        Rect area = _root.rect;
        float rightX = card.xMax + buttonGap;
        float leftX = card.xMin - buttonGap;
        bool fitsRight = rightX + promptWidth <= area.xMax - screenMargin;
        bool fitsLeft = leftX - promptWidth >= area.xMin + screenMargin;
        _promptOnLeft = _onLeft ? (fitsLeft || !fitsRight) : (!fitsRight && fitsLeft);

        _prompt.pivot = new Vector2(_promptOnLeft ? 1f : 0f, 1f);
        float x = _promptOnLeft ? leftX : rightX;
        x = _promptOnLeft
            ? Mathf.Max(x, area.xMin + screenMargin + promptWidth)
            : Mathf.Min(x, area.xMax - screenMargin - promptWidth);
        _prompt.anchoredPosition = new Vector2(x + shake, y);
    }

    private Rect _lastCardRect;
    private bool _promptOnLeft;

    // ---- the card's words ---------------------------------------------------

    private void UpdateSpots()
    {
        _spots.Clear();
        _spotRects.Clear();
        if (!_helpMode || _card == null) return;

        _card.CollectWordSpots(_spots);
        for (int i = 0; i < _spots.Count; i++)
            _spotRects.Add(RootRect(_spots[i].bottomLeft, _spots[i].topRight));
    }

    // Signs win over plain text, and a small target over a big one around it.
    private int HitTest(Vector2 screenPos, Camera cam)
    {
        UpdateSpots();

        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screenPos, cam, out local)) return -1;

        int best = -1;
        float bestArea = float.MaxValue;
        bool bestSign = false;
        for (int i = 0; i < _spotRects.Count; i++)
        {
            Rect r = Grow(_spotRects[i], 4f);
            if (!r.Contains(local)) continue;

            bool sign = _spots[i].isSign;
            float area = r.width * r.height;
            if (best < 0 || (sign && !bestSign) || (sign == bestSign && area < bestArea))
            {
                best = i;
                bestArea = area;
                bestSign = sign;
            }
        }
        return best;
    }

    private int HoveredSpot()
    {
        if (!_helpMode || _card == null) return -1;
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, Input.mousePosition, EventCamera(), out local)) return -1;

        int best = -1;
        for (int i = 0; i < _spotRects.Count; i++)
        {
            if (!Grow(_spotRects[i], 4f).Contains(local)) continue;
            if (best < 0 || (_spots[i].isSign && !_spots[best].isSign)) best = i;
        }
        return best;
    }

    // Every word that can be picked glows, on a cycle: the glow comes up to show
    // what can be clicked, holds, then fades out completely for a while so it
    // never sits over the words while the player is reading them. The word under
    // the pointer and the picked word glow steadily and a little wider.
    private void AnimateHighlights()
    {
        int used = 0;
        if (_helpMode && _card != null)
        {
            int hovered = HoveredSpot();
            float fade = EaseOutCubic(_promptShow);
            float cycle = GlowCycle(_helpTime);

            for (int i = 0; i < _spots.Count; i++)
            {
                bool isHovered = i == hovered;
                bool isPicked = _hasPick && SameSpot(_spots[i], _pick);
                bool steady = isHovered || isPicked;

                float alpha = (steady ? 1f : cycle) * fade;
                if (alpha <= 0.005f) continue;

                float grow = (_spots[i].isSign ? 3f : 2f) + (steady ? 3f : 0f);
                PlaceRing(Ring(used++), Grow(_spotRects[i], grow + GlowPad), alpha);
            }
        }

        for (int i = used; i < _rings.Count; i++) _rings[i].gameObject.SetActive(false);
    }

    [Header("Word glow cycle (seconds)")]
    [SerializeField] private float glowIn = 0.35f;
    [SerializeField] private float glowHold = 1.1f;
    [SerializeField] private float glowOut = 0.5f;
    [Tooltip("How long the glow stays fully transparent before it comes back.")]
    [SerializeField] private float glowOff = 1.6f;

    // 0..1 visibility of the idle glow at time t since help mode opened.
    private float GlowCycle(float t)
    {
        float period = glowIn + glowHold + glowOut + glowOff;
        float c = period > 0f ? t % period : 0f;
        if (c < glowIn) return Mathf.SmoothStep(0f, 1f, c / Mathf.Max(0.01f, glowIn));
        c -= glowIn;
        if (c < glowHold) return 0.85f + 0.15f * Mathf.Sin(c / Mathf.Max(0.01f, glowHold) * Mathf.PI);
        c -= glowHold;
        if (c < glowOut) return Mathf.SmoothStep(1f, 0f, c / Mathf.Max(0.01f, glowOut));
        return 0f;
    }

    private static void PlaceRing(Image ring, Rect r, float alpha)
    {
        ring.rectTransform.anchoredPosition = r.center;
        ring.rectTransform.sizeDelta = r.size;
        ring.color = new Color(1f, 1f, 1f, alpha);
    }

    private static bool SameSpot(CardDisplay.WordSpot a, CardDisplay.WordSpot b)
    {
        return a.key == b.key && a.role == b.role && (a.bottomLeft - b.bottomLeft).sqrMagnitude < 1f;
    }

    // ---- the word card ---------------------------------------------------------

    private Sprite[] SignsFor(string key)
    {
        SignWritingLibrary library = Loc.Signs;
        if (library == null && _card != null) library = _card.signLibrary;
        if (library == null || string.IsNullOrEmpty(key)) return null;
        Sprite[] signs = library.Get(key);
        return signs != null && signs.Length > 0 ? signs : null;
    }

    private void OpenWordCard(CardDisplay.WordSpot spot)
    {
        _pick = spot;
        _hasPick = true;
        _wordTime = 0f;
        _translated = false;

        Sprite[] signs = SignsFor(spot.key);
        _pickIsSign = spot.isSign && signs != null;
        bool hasTranslation = _pickIsSign || signs != null;

        // Large enough to read: the sign itself, or the written word.
        _wordHeading.text = Loc.T(_pickIsSign ? "help.heading_sign" : "help.heading_text");
        _wordBigText.gameObject.SetActive(!_pickIsSign);
        _wordBigSigns.gameObject.SetActive(_pickIsSign);
        if (_pickIsSign) FillSigns(_wordBigSigns, _bigSignPool, signs, bigSignHeight);
        else _wordBigText.text = spot.portuguese;
        _wordRole.text = spot.role;

        // The other language, hidden until asked for.
        _transHeading.text = Loc.T(_pickIsSign ? "help.heading_text" : "help.heading_signwriting");
        _transText.gameObject.SetActive(_pickIsSign);
        _transSigns.gameObject.SetActive(!_pickIsSign);
        if (_pickIsSign) _transText.text = spot.portuguese;
        else if (signs != null) FillSigns(_transSigns, _transSignPool, signs, translationSignHeight);
        _trans.gameObject.SetActive(false);

        _translateButton.gameObject.SetActive(hasTranslation);
        SetActionLabel(_translateButton, TranslateLabel());
        _noSign.gameObject.SetActive(!hasTranslation);

        LayoutRebuilder.ForceRebuildLayoutImmediate(_word);
    }

    private string TranslateLabel()
    {
        if (_translated) return Loc.Text("help.hide");
        return Loc.Text(_pickIsSign ? "help.translate" : "help.show_sign");
    }

    private void OnTranslatePressed()
    {
        _translated = !_translated;
        _transTime = 0f;
        _trans.gameObject.SetActive(_translated);
        SetActionLabel(_translateButton, TranslateLabel());
        LayoutRebuilder.ForceRebuildLayoutImmediate(_word);
    }

    private void CloseWordCard()
    {
        _hasPick = false;
    }

    private void AnimateWordCard(float dt)
    {
        bool show = _helpMode && _hasPick && _card != null;
        _word.gameObject.SetActive(show);
        if (!show) return;

        _wordTime += dt;
        float p = Mathf.Clamp01(_wordTime / 0.22f);
        _wordGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(p * 1.6f));
        _word.localScale = Vector3.one * Mathf.LerpUnclamped(0.85f, 1f, EaseOutBack(p));

        if (_translated)
        {
            _transTime += dt;
            float q = Mathf.Clamp01(_transTime / 0.2f);
            _transGroup.alpha = q;
            _trans.localScale = new Vector3(1f, Mathf.LerpUnclamped(0.6f, 1f, EaseOutBack(q)), 1f);
        }

        PlaceWordCard();
    }

    // Beside the card, on the side away from the Ajuda button, level with the
    // picked word. If that side has no room it goes on the other side, under the
    // prompt.
    private void PlaceWordCard()
    {
        CardDisplay.WordSpot spot = _pick;
        for (int i = 0; i < _spots.Count; i++)
            if (SameSpot(_spots[i], _pick)) { spot = _spots[i]; break; }

        Rect card = RootRect((RectTransform)_card.transform);
        Rect target = RootRect(spot.bottomLeft, spot.topRight);
        Rect area = _root.rect;
        Vector2 half = _word.rect.size * 0.5f;
        const float gap = 20f;

        float left = card.xMin - gap - half.x;
        float right = card.xMax + gap + half.x;
        bool fitsLeft = left - half.x >= area.xMin + screenMargin;
        bool fitsRight = right + half.x <= area.xMax - screenMargin;

        bool goLeft = !_onLeft ? fitsLeft || !fitsRight : !fitsRight && fitsLeft;
        float x = goLeft ? left : right;
        if (!fitsLeft && !fitsRight) x = card.center.x;
        x = Mathf.Clamp(x, area.xMin + screenMargin + half.x, area.xMax - screenMargin - half.x);

        float y = target.center.y;
        bool besideButton = goLeft == _promptOnLeft;
        if (besideButton && _prompt.gameObject.activeSelf)
            y = Mathf.Min(y, _prompt.anchoredPosition.y - _prompt.rect.height - gap - half.y);
        y = Mathf.Clamp(y, area.yMin + screenMargin + half.y, area.yMax - screenMargin - half.y);

        _word.anchoredPosition = new Vector2(x, y);
    }

    private static void FillSigns(RectTransform row, List<Image> pool, Sprite[] signs, float height)
    {
        int count = signs != null ? signs.Length : 0;
        while (pool.Count < count)
        {
            var img = NewImage("Sign", row, null, Color.white);
            img.preserveAspect = true;
            img.gameObject.AddComponent<LayoutElement>();
            pool.Add(img);
        }

        for (int i = 0; i < pool.Count; i++)
        {
            bool used = i < count;
            pool[i].gameObject.SetActive(used);
            if (!used) continue;

            Sprite s = signs[i];
            pool[i].sprite = s;
            float aspect = s != null && s.rect.height > 0f ? s.rect.width / s.rect.height : 1f;
            var le = pool[i].GetComponent<LayoutElement>();
            le.preferredWidth = le.minWidth = height * aspect;
            le.preferredHeight = le.minHeight = height;
        }
    }

    // ---- coordinates ---------------------------------------------------------

    private Camera EventCamera()
    {
        if (_canvas == null || _canvas.renderMode == RenderMode.ScreenSpaceOverlay) return null;
        return _canvas.worldCamera;
    }

    private Rect RootRect(RectTransform rt)
    {
        rt.GetWorldCorners(_corners);
        Vector2 min = _root.InverseTransformPoint(_corners[0]);
        Vector2 max = min;
        for (int i = 1; i < 4; i++)
        {
            Vector2 p = _root.InverseTransformPoint(_corners[i]);
            min = Vector2.Min(min, p);
            max = Vector2.Max(max, p);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    private Rect RootRect(Vector3 worldA, Vector3 worldB)
    {
        Vector2 a = _root.InverseTransformPoint(worldA);
        Vector2 b = _root.InverseTransformPoint(worldB);
        return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
    }

    private static Rect Grow(Rect r, float by)
    {
        return new Rect(r.x - by, r.y - by, r.width + by * 2f, r.height + by * 2f);
    }

    private static float EaseOutCubic(float p)
    {
        p = 1f - Mathf.Clamp01(p);
        return 1f - p * p * p;
    }

    private static float EaseOutBack(float p)
    {
        p = Mathf.Clamp01(p) - 1f;
        const float s = 1.7f;
        return 1f + p * p * ((s + 1f) * p + s);
    }

    // ---- building the UI -------------------------------------------------------

    private void Build()
    {
        if (_root != null) return;

        _canvas = GetComponentInParent<Canvas>();
        if (font == null) font = TMP_Settings.defaultFontAsset;

        _root = NewRect("CardHelp", transform);
        Stretch(_root);

        _highlights = NewRect("Highlights", _root);
        Stretch(_highlights);

        BuildButton();
        BuildPrompt();
        BuildWordCard();
    }

    private void BuildButton()
    {
        _button = NewRect("HelpButton", _root);
        _button.sizeDelta = buttonSize;
        _buttonGroup = _button.gameObject.AddComponent<CanvasGroup>();

        _buttonPlate = _button.gameObject.AddComponent<Image>();
        _buttonPlate.sprite = PlateSprite();
        _buttonPlate.type = Image.Type.Sliced;
        _buttonPlate.color = plateColor;
        _buttonPlate.raycastTarget = true;

        // Clips the glint to the button's rounded shape.
        _button.gameObject.AddComponent<Mask>().showMaskGraphic = true;

        var button = _button.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(ToggleHelp);

        _buttonShine = NewImage("Glint", _button, BandSprite(), Color.clear);
        _buttonShine.rectTransform.sizeDelta = new Vector2(buttonSize.y * 0.9f, buttonSize.y * 1.8f);
        _buttonShine.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -20f);

        _buttonRim = NewImage("Rim", _button, RingSprite(), rimColor);
        _buttonRim.type = Image.Type.Sliced;
        Stretch(_buttonRim.rectTransform);

        // "?" medallion on the left, the word on the right.
        float d = buttonSize.y - 20f;
        _icon = NewRect("Icon", _button);
        _icon.sizeDelta = new Vector2(d, d);
        _icon.anchorMin = _icon.anchorMax = new Vector2(0f, 0.5f);
        _icon.anchoredPosition = new Vector2(12f + d * 0.5f, 0f);
        var disc = _icon.gameObject.AddComponent<Image>();
        disc.sprite = CircleSprite();
        disc.color = accentColor;
        disc.raycastTarget = false;
        var q = NewText("Mark", _icon, "?", d * 0.72f, plateColor, TextAlignmentOptions.Center);
        q.fontStyle = FontStyles.Bold;
        Stretch(q.rectTransform);

        var label = NewText("Label", _button, Loc.Signed("help.button", SignText.Layout.Inline, 1.2f), 26f, textColor, TextAlignmentOptions.Center);
        SignText.UsePlainSigns(label);
        var lrt = label.rectTransform;
        lrt.anchorMin = new Vector2(0f, 0f);
        lrt.anchorMax = new Vector2(1f, 1f);
        lrt.offsetMin = new Vector2(12f + d + 4f, 0f);
        lrt.offsetMax = new Vector2(-10f, 0f);

        _button.gameObject.SetActive(false);
    }

    private void BuildPrompt()
    {
        _prompt = NewRect("Prompt", _root);
        _prompt.pivot = new Vector2(0.5f, 1f);
        _prompt.sizeDelta = new Vector2(promptWidth, 0f);
        _promptGroup = _prompt.gameObject.AddComponent<CanvasGroup>();
        _promptGroup.blocksRaycasts = false;

        // Same look as the Ajuda button it hangs from: dark plate, gold rim.
        Panel(_prompt, plateColor, rimColor);

        var layout = _prompt.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 14, 14);
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fit = _prompt.gameObject.AddComponent<ContentSizeFitter>();
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        var main = NewText("Ask", _prompt, Loc.Text("help.prompt"), 22f, textColor, TextAlignmentOptions.Center);
        main.enableWordWrapping = true;
        var hint = NewText("Hint", _prompt, Loc.Text("help.exit_hint"), 18f, promptHintColor, TextAlignmentOptions.Center);
        hint.enableWordWrapping = true;

        _prompt.gameObject.SetActive(false);
    }

    private void BuildWordCard()
    {
        _word = NewRect("WordCard", _root);
        _wordGroup = _word.gameObject.AddComponent<CanvasGroup>();

        // Opaque and raycast-blocking: its buttons take clicks, and a click on
        // the panel itself must not fall through to the card underneath.
        var plate = Panel(_word, panelColor, panelBorderColor);
        plate.raycastTarget = true;

        var layout = _word.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(24, 24, 18, 20);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fit = _word.gameObject.AddComponent<ContentSizeFitter>();
        fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _wordHeading = PanelText("Heading", _word, "", 18f, headingColor);
        _wordHeading.characterSpacing = 4f;
        _wordHeading.fontStyle = FontStyles.Bold;
        _wordHeading.gameObject.AddComponent<LayoutElement>().minWidth = wordCardMinWidth - 48f;

        _wordBigText = PanelText("Word", _word, "", 64f, inkColor);
        _wordBigSigns = SignRowRect("Signs", _word);

        _wordRole = PanelText("Role", _word, "", 20f, inkMutedColor);

        _trans = NewRect("Translation", _word);
        _transGroup = _trans.gameObject.AddComponent<CanvasGroup>();
        _trans.pivot = new Vector2(0.5f, 1f);
        Panel(_trans, tileColor, panelBorderColor);
        var tl = _trans.gameObject.AddComponent<VerticalLayoutGroup>();
        tl.padding = new RectOffset(16, 16, 12, 14);
        tl.spacing = 6f;
        tl.childAlignment = TextAnchor.UpperCenter;
        tl.childControlWidth = tl.childControlHeight = true;
        tl.childForceExpandWidth = true;
        tl.childForceExpandHeight = false;
        _transHeading = PanelText("Heading", _trans, "", 16f, headingColor);
        _transHeading.characterSpacing = 3f;
        _transHeading.fontStyle = FontStyles.Bold;
        _transText = PanelText("Word", _trans, "", 44f, inkColor);
        _transSigns = SignRowRect("Signs", _trans);
        _trans.gameObject.SetActive(false);

        _noSign = PanelText("NoSign", _word, Loc.Text("help.no_sign"), 18f, inkMutedColor);
        _noSign.enableWordWrapping = true;

        var buttons = NewRect("Buttons", _word);
        var bl = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
        bl.spacing = 12f;
        bl.childAlignment = TextAnchor.MiddleCenter;
        bl.childControlWidth = bl.childControlHeight = true;
        bl.childForceExpandWidth = bl.childForceExpandHeight = false;
        _translateButton = ActionButton("Translate", buttons, Loc.Text("help.show_sign"), OnTranslatePressed);
        ActionButton("Close", buttons, Loc.Signed("help.close"), CloseWordCard);

        _word.gameObject.SetActive(false);
    }

    private static RectTransform SignRowRect(string objectName, Transform parent)
    {
        var row = NewRect(objectName, parent);
        var h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 10f;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = h.childControlHeight = true;
        h.childForceExpandWidth = h.childForceExpandHeight = false;
        return row;
    }

    // An opaque rounded panel with a dark border drawn over its edge.
    private Image Panel(RectTransform rt, Color fill, Color border)
    {
        var plate = rt.gameObject.AddComponent<Image>();
        plate.sprite = PlateSprite();
        plate.type = Image.Type.Sliced;
        plate.color = fill;
        plate.raycastTarget = false;

        var rim = NewImage("Border", rt, RingSprite(), border);
        rim.type = Image.Type.Sliced;
        rim.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        Stretch(rim.rectTransform);
        return plate;
    }

    private Button ActionButton(string objectName, Transform parent, string label, UnityAction onClick)
    {
        var rt = NewRect(objectName, parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = PlateSprite();
        img.type = Image.Type.Sliced;
        img.color = Color.white;          // the button's colour tint paints it
        img.raycastTarget = true;

        var le = rt.gameObject.AddComponent<LayoutElement>();
        le.minHeight = le.preferredHeight = 52f;   // a comfortable target, over the 44px guideline

        var button = rt.gameObject.AddComponent<Button>();
        var colors = button.colors;
        colors.normalColor = actionColor;
        colors.highlightedColor = actionHoverColor;
        colors.pressedColor = Color.Lerp(actionColor, Color.black, 0.3f);
        colors.selectedColor = actionColor;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.targetGraphic = img;
        button.onClick.AddListener(onClick);

        var text = PanelText("Label", rt, label, 20f, actionTextColor);
        text.fontStyle = FontStyles.Bold;
        Stretch(text.rectTransform);

        SetActionLabel(button, label);
        return button;
    }

    private static void SetActionLabel(Button button, string label)
    {
        var text = button.GetComponentInChildren<TMP_Text>(true);
        text.text = label;
        var le = button.GetComponent<LayoutElement>();
        le.minWidth = le.preferredWidth = Mathf.Max(150f, text.GetPreferredValues(label).x + 40f);
    }

    private Image Ring(int index)
    {
        while (_rings.Count <= index)
        {
            var img = NewImage("Outline", _highlights, FocusSprite(), Color.white);
            img.type = Image.Type.Sliced;
            _rings.Add(img);
        }
        Image ring = _rings[index];
        ring.gameObject.SetActive(true);
        return ring;
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

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static Image NewImage(string objectName, Transform parent, Sprite sprite, Color color)
    {
        var img = NewRect(objectName, parent).gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private TMP_Text NewText(string objectName, Transform parent, string text, float size, Color color,
                             TextAlignmentOptions align)
    {
        var t = NewRect(objectName, parent).gameObject.AddComponent<TextMeshProUGUI>();
        SignText.UsePlainSigns(t);   // panel text in Libras mode: thin signs
        if (font != null) t.font = font;
        if (fontMaterial != null) t.fontSharedMaterial = fontMaterial;
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.enableWordWrapping = false;
        t.raycastTarget = false;
        return t;
    }

    // Text on the light panels: the font's own material, without the drop
    // shadow the HUD material adds - dark ink on a light panel is already crisp,
    // and the shadow only smudges it.
    private TMP_Text PanelText(string objectName, Transform parent, string text, float size, Color color)
    {
        var t = NewText(objectName, parent, text, size, color, TextAlignmentOptions.Center);
        if (font != null) t.fontSharedMaterial = font.material;
        return t;
    }

    // ---- procedural sprites ------------------------------------------------

    private const int SpriteSize = 64;
    private const float Corner = 18f;

    // Filled rounded rectangle, 9-sliced so the corners hold at any size.
    public static Sprite PlateSprite()
    {
        if (_plateSprite == null)
            _plateSprite = RoundedSprite("HelpPlate", Corner, d => new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d)));
        return _plateSprite;
    }

    // Outline of the same rounded rectangle.
    public static Sprite RingSprite()
    {
        if (_ringSprite == null)
            _ringSprite = RoundedSprite("HelpRing", Corner, d => new Color(1f, 1f, 1f, Mathf.Clamp01(1.6f - Mathf.Abs(d + 2.2f))));
        return _ringSprite;
    }

    private static Sprite CircleSprite()
    {
        if (_circleSprite == null)
            _circleSprite = RoundedSprite("HelpCircle", SpriteSize * 0.5f, d => new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d)));
        return _circleSprite;
    }

    // How far the glow's halo reaches past the word's box; the ring images are
    // grown by this much so the bright line itself sits on the box.
    private const float GlowPad = 12f;

    // The word glow: a thin bright line with a soft halo spreading outward, and
    // nothing at all inside it, so the word itself stays exactly as it was.
    private Sprite FocusSprite()
    {
        if (_focusSprite != null) return _focusSprite;

        Color line = Color.Lerp(focusColor, Color.white, 0.45f);
        Color halo = focusColor;
        _focusSprite = RoundedSprite("HelpGlow", 8f, d =>
        {
            float l = Mathf.Clamp01(1.4f - Mathf.Abs(d + 0.6f));
            float h = d > 0f ? 0.65f * Mathf.Exp(-d / 4.5f) * Mathf.Clamp01((GlowPad - d) / 3f) : 0f;
            float a = Mathf.Max(l, h);
            Color c = a > 0f ? Color.Lerp(halo, line, l / a) : halo;
            c.a = a;
            return c;
        }, GlowPad);
        return _focusSprite;
    }

    private static Sprite BandSprite()
    {
        if (_bandSprite != null) return _bandSprite;

        const int w = 32, h = 4;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w - 0.5f;
                px[y * w + x] = new Color32(255, 255, 255, (byte)(255f * Mathf.Exp(-u * u / 0.025f)));
            }
        tex.SetPixels32(px);
        tex.Apply();
        _bandSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        return _bandSprite;
    }

    // colorAt receives the signed distance in pixels to the rounded rectangle's
    // edge: negative inside, positive outside.
    private static Sprite RoundedSprite(string spriteName, float radius, System.Func<float, Color> colorAt,
                                        float inset = 0f)
    {
        const int size = SpriteSize;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = spriteName,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var px = new Color32[size * size];
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float qx = Mathf.Abs(x + 0.5f - half) - (half - inset - radius);
                float qy = Mathf.Abs(y + 0.5f - half) - (half - inset - radius);
                float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
                float d = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                px[y * size + x] = colorAt(d);
            }
        }
        tex.SetPixels32(px);
        tex.Apply();

        float border = Mathf.Min(inset + radius + 2f, half - 1f);
        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                                   SpriteMeshType.FullRect, new Vector4(border, border, border, border));
        sprite.name = spriteName;
        return sprite;
    }
}

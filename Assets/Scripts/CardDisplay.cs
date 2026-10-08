using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Video;

public class CardDisplay : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public CardDataSo card;

    // True while a card is selected (raised/enlarged) and awaiting a
    // double-click confirmation to actually be used.
    public bool IsSelected => CardManager.instance != null && CardManager.instance.selectedCard == this;
    public bool IsHovered { get; private set; }

    // How long the pointer must sit on a card before it counts as a
    // deliberate hover (and greys out the rest of the hand) rather than the
    // mouse just passing over it while moving across the fan.
    [SerializeField] private float hoverHoldTime = 0.35f;
    private float _hoverTimer;

    // True once the pointer has hovered this card continuously for at least hoverHoldTime.
    public bool IsHoverHeld => IsHovered && _hoverTimer >= hoverHoldTime;

    [Tooltip("How far into shadow a card is pushed while another card is raised. " +
             "0 leaves it untouched, 1 is solid black.")]
    [Range(0f, 1f)] [SerializeField] private float dimStrength = 0.6f;

    private Image _dimOverlay;
    private float _dim;

    private void Update()
    {
        _hoverTimer = IsHovered ? _hoverTimer + Time.unscaledDeltaTime : 0f;
    }

    // Eases this card toward target (0 = lit, 1 = shadowed) by t, and applies it.
    // Driven from CardFanLayout so the whole hand shares one easing rate.
    public void StepDim(float target, float t)
    {
        _dim = Mathf.Lerp(_dim, Mathf.Clamp01(target), t);

        if (_dimOverlay == null && !BuildDimOverlay()) return;

        Color c = _dimOverlay.color;
        c.a = _dim * dimStrength;
        _dimOverlay.color = c;
    }

    // A plain black quad on top of the card art, inside MaskedBody so the card's
    // rounded corners clip it the same way they clip everything else.
    private bool BuildDimOverlay()
    {
        Transform body = transform.Find("MaskedBody");
        Transform parent = body != null ? body : transform;

        Transform existing = parent.Find("Dim");
        if (existing != null) _dimOverlay = existing.GetComponent<Image>();

        if (_dimOverlay == null)
        {
            var go = new GameObject("Dim", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            _dimOverlay = go.AddComponent<Image>();
            _dimOverlay.raycastTarget = false;   // must never swallow a click on the card
        }

        _dimOverlay.color = new Color(0f, 0f, 0f, 0f);
        _dimOverlay.transform.SetAsLastSibling();
        return true;
    }
    
    [Header("UI Layers")]
    public Image backgroundImage; // The very back image
    public RawImage videoDisplay; // 3D model animation
    public Image frameImage;      // The front transparent cutout frame
    
    [Header("Card Info")]
    public TMP_Text cardNameText;
    public TMP_Text cardDescriptionText;
    public TMP_Text cardTypeText;
    public TMP_Text cardStaminaText;

    [Header("Video Settings")]
    public VideoPlayer videoCard;
    private RenderTexture uniqueRenderTexture;

    [Header("Card Data")] 
    public string cardName;
    public string cardDesc;
    public CardTypes cardType;
    public int cardStamina;

    public int strength;
    public int defense;
    public int cardDraw;
    public int cure;

    [Header("Audio & VFX")]
    [SerializeField] private AudioClip _attackSFX;
    [SerializeField] private AudioClip _defenseSFX;
    [SerializeField] private AudioClip _itemSFX;
    [SerializeField] private GameObject _particle;

    private AudioSource _audioSource;
    
    // Optional: Arrays/Dictionaries to hold your different background/frame sprites
    [Header("SignWriting")]
    [Tooltip("Maps words to their SignWriting sign sprites.")]
    public SignWritingLibrary signLibrary;

    // The active sign language's signs (see Loc); the reference above is only
    // the fallback for a scene without locale settings.
    private SignWritingLibrary Signs
    {
        get { var active = Loc.Signs; return active != null ? active : signLibrary; }
    }
    [Tooltip("Renders the card's name as signs. Falls back to text when a sign is missing.")]
    public SignRow nameSignRow;
    [Tooltip("Renders the card's category (e.g. carta comum = two signs).")]
    public SignRow categorySignRow;

    [Header("Art Assets")]
    public Sprite bgAttack;
    public Sprite bgDefense;
    public Sprite bgItem;

    [Header("Card Frame")]
    [Tooltip("The ornate frame behind everything, chosen by card type.")]
    public Sprite frameAttack;
    public Sprite frameDefense;
    [Tooltip("Cure / item card frame.")]
    public Sprite frameItem;

    [Header("Card Border")]
    [Tooltip("The outline ring behind the card body. Tinted to a darker shade " +
             "of this card's own type colour, so an attack card is edged in deep " +
             "red, a defence card in deep blue, and so on.")]
    public Image borderImage;
    [Tooltip("How far the type colour is pushed toward black for the border. " +
             "1 leaves it at full brightness.")]
    [Range(0.05f, 1f)] public float borderDarken = 0.35f;

    [Header("Energy Cost Badge")]
    public Image energyBadgeImage;
    public Sprite energyBadge;

    [Header("Effect Badges")]
    [Tooltip("Badge slots to the right of the card type, filled with whatever the " +
             "card actually does. Unused slots hide themselves.")]
    public Image[] actionBadges;
    [Tooltip("The number drawn on each badge slot, in the same order.")]
    public TMP_Text[] actionBadgeLabels;
    public Sprite attackBadge;
    public Sprite defenseBadge;
    [Tooltip("Cure card action badge art.")]
    public Sprite cureBadge;

    [Header("Inline Signs")]
    [Tooltip("How much bigger a sign is than the word it replaced, inside the " +
             "description. 1 = same size, which is too small to read.")]
    [Range(1f, 3f)] public float inlineSignScale = 1.8f;
    
    private void Start()
    {
        _audioSource = GetComponent<AudioSource>();
        CollectInfoFromCardSo();
    }

    private void CollectInfoFromCardSo()
    {
        if (card == null)
        {
            Destroy(gameObject);
            return;
        }
        
        if (card.cardVideo == null)
        {
            Debug.LogWarning("Card video is not assigned!");
        }

        cardName = Loc.CardName(card);   // in the active language; see Assets/Locales
        cardType = card.type;
        cardStamina = card.cardStamina;
        strength = card.strength;
        defense = card.defense;
        cardDraw = card.cardDrawAmount;
        cure = card.cure;

        UpdateDisplay();
    }

    public void UpdateDisplay()
    {
        ApplyNameSigns();
        ApplyCategorySigns();
        
        // Stamina cost: numeral until its sign has been learned.
        cardStaminaText.text = StagedInlineBadge("Numero_" + cardStamina, cardStamina.ToString());

        cardDesc = ProcessDescription(Loc.CardDescription(card));
        cardDescriptionText.text = cardDesc;

        UpdateCardArtLayers();
        PlayVideo(card.cardVideo);
    }
    

    // Words start as plain Portuguese and grow into SignWriting as the player
    // uses that card, so a hand can hold a mix of text, half-learned and fully
    // signed cards rather than flipping over all at once.
    private void ApplyStagedWord(string word, string fallbackText, TMP_Text label, SignRow row)
    {
        Sprite[] signs = Signs != null ? Signs.Get(word) : null;
        var stage = SignProgression.GetStage(word);

        // No sign authored for this word yet - always show the text.
        if (signs == null || signs.Length == 0 || row == null)
        {
            if (row != null) row.Clear();
            if (label != null) { label.text = fallbackText; label.alpha = 1f; }
            return;
        }

        if (stage == SignProgression.Stage.Sign)
        {
            row.SetSigns(signs);
            if (label != null) { label.text = string.Empty; label.alpha = 1f; }
        }
        else
        {
            row.Clear();
            if (label != null) { label.text = fallbackText; label.alpha = 1f; }
        }
    }

    private void ApplyNameSigns()
    {
        if (SignProgression.CardNamesStayPortuguese)
        {
            if (nameSignRow != null) nameSignRow.Clear();
            if (cardNameText != null) { cardNameText.text = cardName; cardNameText.alpha = 1f; }
            return;
        }
        ApplyStagedWord(cardName, cardName, cardNameText, nameSignRow);
    }

    private string CategoryKey()
    {
        return card != null && card.cardCategory == CardCategories.COMBINACAO ? "categories.combination" : "categories.common";
    }

    private string CategoryText() { return Loc.T(CategoryKey()); }

    // The dictionary entry the category is signed with (Carta_Comum is two signs).
    private string CategorySignWord()
    {
        return Loc.Gloss(CategoryKey()) ?? CategoryText();
    }

    // A category can span several signs - "carta comum" is two - so this always
    // goes through the row rather than a single glyph.
    private void ApplyCategorySigns()
    {
        bool combo = card != null && card.cardCategory == CardCategories.COMBINACAO;
        string word = CategorySignWord();
        string text = CategoryText();
        ApplyStagedWord(word, text, cardTypeText, categorySignRow);
    }

    // Attack and defence have their own ornate frame; cure cards have no art of
    // Each card type has its own ornate frame and badge art.
    private void UpdateCardArtLayers()
{
    // ---- Background PNG ----
    // A card may name its own background; otherwise it falls back to the art for
    // its type, the same way the frame below does. Without this fallback every
    // card kept whatever background the prefab shipped with, so an attack card
    // and a cure card looked identical.
    if (backgroundImage != null)
    {
        Sprite bg = card.cardBackground;
        if (bg == null)
        {
            switch (card.type)
            {
                case CardTypes.ATAQUE: bg = bgAttack; break;
                case CardTypes.DEFESA: bg = bgDefense; break;
                case CardTypes.ITEM:   bg = bgItem != null ? bgItem : bgDefense; break;
                default:               bg = bgDefense; break;
            }
        }
        if (bg != null) backgroundImage.sprite = bg;
    }

    if (frameImage != null)
    {
        Sprite frame;

        switch (card.type)
        {
            case CardTypes.ATAQUE: frame = frameAttack; break;
            case CardTypes.DEFESA: frame = frameDefense; break;
            case CardTypes.ITEM:   frame = frameItem != null ? frameItem : frameDefense; break;
            default:               frame = frameDefense; break;
        }

        if (frame != null) frameImage.sprite = frame;
        // Every card type now has its own frame art, so the frame is never tinted.
        frameImage.color = Color.white;
    }

    if (energyBadgeImage != null && energyBadge != null) energyBadgeImage.sprite = energyBadge;

    UpdateActionBadges();

    // Tell the border halo which colour to bloom (red / blue / green by type).
    var shine = GetComponentInChildren<CardShine>(true);
    if (shine != null) shine.SetType(card.type);

    // The outline takes the same type colour, pushed down toward black so it
    // reads as this card's own edge rather than a second glow.
    if (borderImage != null && shine != null)
    {
        Color t = shine.ColorFor(card.type);
        borderImage.color = new Color(t.r * borderDarken, t.g * borderDarken, t.b * borderDarken, 1f);
    }
}
    // The energy badge says what the card costs; these say what it does. A card
    // can do more than one thing (Agua both defends and heals), so the badges are
    // filled from the non-zero values rather than from the card's type.
    private void UpdateActionBadges()
    {
        if (actionBadges == null) return;

        int slot = 0;
        if (strength > 0) slot = ShowBadge(slot, attackBadge, Color.white, strength, Loc.T("card_parts.damage"));
        if (defense > 0) slot = ShowBadge(slot, defenseBadge, Color.white, defense, Loc.T("card_parts.defense"));
        if (cure > 0) slot = ShowBadge(slot, cureBadge != null ? cureBadge : defenseBadge, Color.white, cure, Loc.T("card_parts.cure"));

        for (int i = slot; i < actionBadges.Length; i++)
            if (actionBadges[i] != null) actionBadges[i].gameObject.SetActive(false);
    }

    private int ShowBadge(int slot, Sprite sprite, Color tint, int value, string role)
    {
        if (slot >= actionBadges.Length || actionBadges[slot] == null) return slot;

        var img = actionBadges[slot];
        img.gameObject.SetActive(true);
        if (sprite != null) img.sprite = sprite;
        img.color = tint;

        if (actionBadgeLabels != null && slot < actionBadgeLabels.Length && actionBadgeLabels[slot] != null)
            actionBadgeLabels[slot].text = StagedInlineBadge("Numero_" + value, value.ToString());

        while (_badgeValues.Count <= slot) { _badgeValues.Add(0); _badgeRoles.Add(null); }
        _badgeValues[slot] = value;
        _badgeRoles[slot] = role;

        return slot + 1;
    }

    // What each badge slot currently shows, for reading the card back.
    private readonly List<int> _badgeValues = new List<int>();
    private readonly List<string> _badgeRoles = new List<string>();

    // Starts on the prepareCompleted callback rather than a coroutine polling
    // isPrepared: a card is re-parented between the draw pile, the hand and the
    // discard pile while that coroutine is still waiting, and it did not reliably
    // survive to call Play(). The render texture is allocated once per card
    // rather than once per UpdateDisplay, which leaked one on every refresh.
    private void PlayVideo(VideoClip clip)
    {
        if (clip == null || videoCard == null || videoDisplay == null) return;

        if (uniqueRenderTexture == null)
        {
            uniqueRenderTexture = new RenderTexture(864, 1168, 0);
            videoDisplay.texture = uniqueRenderTexture;
            videoCard.targetTexture = uniqueRenderTexture;
        }

        if (videoCard.clip == clip && (videoCard.isPlaying || videoCard.isPaused)) return;

        videoCard.clip = clip;
        videoCard.isLooping = true;
        videoCard.prepareCompleted -= OnVideoPrepared;
        videoCard.prepareCompleted += OnVideoPrepared;

        if (videoCard.isPrepared) videoCard.Play();
        else videoCard.Prepare();
    }

    private void OnVideoPrepared(VideoPlayer source)
    {
        source.Play();
    }

    private void OnDestroy()
    {
        if (videoCard != null) videoCard.prepareCompleted -= OnVideoPrepared;
        if (uniqueRenderTexture != null) uniqueRenderTexture.Release();
    }

    // Everything the player just read on this card gets a little closer to
    // being shown as a sign. Common vocabulary appears on many cards so it
    // matures quickly, while an individual card's name takes longer - which is
    // what staggers the hand instead of flipping it over all at once.
    // Redraws only the wording. Used when a sign graduates mid-run, where calling
    // the full UpdateDisplay would needlessly tear down and re-prepare the video.
    public void RefreshSignText()
    {
        if (card == null) return;
        ApplyNameSigns();
        ApplyCategorySigns();
        cardStaminaText.text = StagedInlineBadge("Numero_" + cardStamina, cardStamina.ToString());
        UpdateActionBadges();   // their numbers graduate too
        cardDesc = ProcessDescription(Loc.CardDescription(card));
        cardDescriptionText.text = cardDesc;
    }

    /// Turns a word that just graduated into its sign under a sweep of light,
    /// so the player sees which word changed. See CardSignReveal.
    public void RevealSign(string word, float delay)
    {
        var reveal = GetComponent<CardSignReveal>();
        if (reveal == null) reveal = gameObject.AddComponent<CardSignReveal>();
        reveal.Play(this, word, delay);
    }

    private readonly List<string> _wordScratch = new List<string>();

    /// Whether this card has the word anywhere on it.
    public bool ShowsWord(string word)
    {
        string key = SignWritingLibrary.Normalize(word);
        _wordScratch.Clear();
        CollectSignWords(_wordScratch);
        for (int i = 0; i < _wordScratch.Count; i++)
            if (SignWritingLibrary.Normalize(_wordScratch[i]) == key) return true;
        return false;
    }

    // ---- reading the card back -----------------------------------------------

    /// One readable piece of the card - a word, a sign, a number - with where it
    /// sits and what it says in Portuguese. The help (Ajuda) uses these to
    /// translate whatever the player points at; the reveal shine uses them to
    /// find the word that just turned into a sign.
    public struct WordSpot
    {
        public string key;            // the SignWriting word behind it, e.g. "Fogo", "Numero_3"
        public string portuguese;     // what it says, e.g. "Fogo", "3 (três)"
        public string role;           // which part of the card, e.g. "Nome da carta"
        public bool isSign;           // drawn as SignWriting right now
        public Vector3 bottomLeft;    // opposite corners, in world space
        public Vector3 topRight;
        public RectTransform element; // the object drawing it, when it has its own
    }

    /// Every readable piece of the card, signs and plain text alike.
    public void CollectWordSpots(List<WordSpot> into)
    {
        if (card == null || into == null) return;

        // Name and category: the sign row while learned, the written word before.
        AddRowOrText(into, nameSignRow, cardNameText, cardName, cardName, Loc.T("card_parts.name"));
        AddRowOrText(into, categorySignRow, cardTypeText, CategorySignWord(), CategoryText(), Loc.T("card_parts.type"));

        AddNumber(into, cardStaminaText, cardStamina, Loc.T("card_parts.energy"));

        if (actionBadges != null)
        {
            for (int i = 0; i < actionBadges.Length && i < _badgeValues.Count; i++)
            {
                if (actionBadges[i] == null || !actionBadges[i].gameObject.activeInHierarchy) continue;
                TMP_Text label = actionBadgeLabels != null && i < actionBadgeLabels.Length ? actionBadgeLabels[i] : null;
                AddNumber(into, label, _badgeValues[i], _badgeRoles[i]);
            }
        }

        AddDescription(into);
    }

    private static void AddRowOrText(List<WordSpot> into, SignRow row, TMP_Text label,
                                     string key, string portuguese, string role)
    {
        Vector3 bl, tr;
        if (row != null && TryRowCorners(row, out bl, out tr))
        {
            into.Add(new WordSpot { key = key, portuguese = portuguese, role = role, isSign = true,
                                    bottomLeft = bl, topRight = tr, element = (RectTransform)row.transform });
            return;
        }

        if (label != null && TryTextCorners(label, out bl, out tr))
            into.Add(new WordSpot { key = key, portuguese = portuguese, role = role, isSign = false,
                                    bottomLeft = bl, topRight = tr, element = label.rectTransform });
    }

    private void AddNumber(List<WordSpot> into, TMP_Text label, int value, string role)
    {
        if (label == null || !label.gameObject.activeInHierarchy) return;

        string key = "Numero_" + value;
        bool isSign = label.text.Contains("<sprite");
        RectTransform rt = label.rectTransform;
        Rect r = rt.rect;
        into.Add(new WordSpot
        {
            key = key,
            portuguese = isSign ? Loc.WordForSign(key) : value.ToString(),
            role = role,
            isSign = isSign,
            bottomLeft = rt.TransformPoint(r.min),
            topRight = rt.TransformPoint(r.max),
            element = rt
        });
    }

    // Each inline sign in the description, and each word still written out.
    private void AddDescription(List<WordSpot> into)
    {
        TMP_Text text = cardDescriptionText;
        if (text == null || !text.gameObject.activeInHierarchy) return;

        TMP_TextInfo info = text.textInfo;
        if (info == null) return;
        Transform t = text.transform;

        int signOrdinal = 0;
        for (int i = 0; i < info.characterCount; i++)
        {
            TMP_CharacterInfo ci = info.characterInfo[i];
            if (ci.elementType != TMP_TextElementType.Sprite) continue;

            string key = SpriteName(ci);
            string written = null;
            if (signOrdinal < _descSigns.Count)
            {
                written = _descSigns[signOrdinal].written;
                key = _descSigns[signOrdinal].key;   // "carta_comum", not its part "Carta_Comum_1"
            }
            signOrdinal++;
            if (string.IsNullOrEmpty(key) || !ci.isVisible) continue;

            bool isNumber = SignWritingLibrary.Normalize(key).StartsWith("numero_");
            into.Add(new WordSpot
            {
                key = key,
                portuguese = isNumber || string.IsNullOrEmpty(written)
                    ? Loc.WordForSign(key)
                    : Capitalize(written),
                role = Loc.T("card_parts.description"),
                isSign = true,
                bottomLeft = t.TransformPoint(ci.bottomLeft),
                topRight = t.TransformPoint(ci.topRight)
            });
        }

        for (int w = 0; w < info.wordCount; w++)
        {
            TMP_WordInfo wi = info.wordInfo[w];
            if (wi.characterCount <= 0) continue;
            TMP_CharacterInfo first = info.characterInfo[wi.firstCharacterIndex];
            TMP_CharacterInfo last = info.characterInfo[wi.lastCharacterIndex];
            if (first.lineNumber >= info.lineCount) continue;

            // Whole line height, so short words are as easy to hit as tall ones.
            TMP_LineInfo line = info.lineInfo[first.lineNumber];
            string word = wi.GetWord();
            into.Add(new WordSpot
            {
                key = SignKeyForWord(word),
                portuguese = word,
                role = Loc.T("card_parts.description"),
                isSign = false,
                bottomLeft = t.TransformPoint(new Vector3(first.bottomLeft.x, line.descender, 0f)),
                topRight = t.TransformPoint(new Vector3(last.topRight.x, line.ascender, 0f))
            });
        }
    }

    /// The SignWriting word for a word written in a description: numerals as
    /// Numero_N, a word with a sign (or a synonym of one, ataque -> Dano) as
    /// that sign's key, and anything else as itself, which has no sign.
    public string SignKeyForWord(string word)
    {
        if (string.IsNullOrEmpty(word)) return word;
        int n;
        if (int.TryParse(word, out n)) return "Numero_" + n;
        string key = Signs != null ? Signs.Resolve(word) : null;
        return key ?? word;
    }

    private static string SpriteName(TMP_CharacterInfo ci)
    {
        var sc = ci.textElement as TMP_SpriteCharacter;
        if (sc != null) return sc.name;
        if (ci.spriteAsset != null && ci.spriteIndex >= 0 && ci.spriteIndex < ci.spriteAsset.spriteCharacterTable.Count)
            return ci.spriteAsset.spriteCharacterTable[ci.spriteIndex].name;
        return null;
    }

    private static string Capitalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return char.ToUpper(s[0]) + s.Substring(1);
    }

    // The signs actually laid out in a row, as one box. False while the row is
    // empty, i.e. the word is still written out.
    private static bool TryRowCorners(SignRow row, out Vector3 bottomLeft, out Vector3 topRight)
    {
        bottomLeft = topRight = Vector3.zero;
        if (!row.gameObject.activeInHierarchy) return false;

        var rowRt = (RectTransform)row.transform;
        var corners = new Vector3[4];
        bool any = false;
        Vector2 min = Vector2.zero, max = Vector2.zero;

        for (int i = 0; i < rowRt.childCount; i++)
        {
            var child = rowRt.GetChild(i) as RectTransform;
            if (child == null || !child.gameObject.activeSelf) continue;

            child.GetWorldCorners(corners);
            for (int c = 0; c < 4; c++)
            {
                Vector2 p = rowRt.InverseTransformPoint(corners[c]);
                if (!any) { min = max = p; any = true; }
                else { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            }
        }

        if (!any) return false;
        bottomLeft = rowRt.TransformPoint(min);
        topRight = rowRt.TransformPoint(max);
        return true;
    }

    private static bool TryTextCorners(TMP_Text label, out Vector3 bottomLeft, out Vector3 topRight)
    {
        bottomLeft = topRight = Vector3.zero;
        if (!label.gameObject.activeInHierarchy || string.IsNullOrEmpty(label.text)) return false;

        Bounds b = label.textBounds;
        if (b.size.x <= 0.01f || b.size.y <= 0.01f) return false;

        bottomLeft = label.transform.TransformPoint(b.min);
        topRight = label.transform.TransformPoint(b.max);
        return true;
    }

    // Every word this card could show as a sign. CardManager gathers these from
    // the whole hand at the start of a turn and lets SignProgression graduate one
    // of them, so the player only ever meets a new sign on a card they can see.
    public void CollectSignWords(List<string> into)
    {
        if (card == null || into == null) return;

        // A name already in Portuguese has nothing left to graduate.
        if (!SignProgression.CardNamesStayPortuguese) AddWord(into, cardName);
        AddWord(into, CategorySignWord());
        AddWord(into, "Numero_" + cardStamina);

        string description = Loc.CardDescription(card);
        if (string.IsNullOrEmpty(description)) return;

        foreach (var raw in description.Split(' '))
        {
            string written;
            AddWord(into, DescriptionWordKey(raw, out written));
        }
    }

    private void AddWord(List<string> into, string word)
    {
        if (string.IsNullOrEmpty(word)) return;
        if (Signs == null || !Signs.Has(word)) return;   // no sign authored, nothing to learn
        if (!into.Contains(word)) into.Add(word);
    }

    // One word of a card description as the player reads it, and the sign
    // behind it. The placeholder letters become the card's own numbers (S is
    // the damage, D the defence, C the cure, X the cards drawn); every other
    // word is looked up in the library, synonyms included, so any word that has
    // a sign gets translated. Null when the word has no sign.
    private string DescriptionWordKey(string raw, out string written)
    {
        written = raw;
        if (string.IsNullOrEmpty(raw)) return null;

        string w = raw.ToUpper();
        int value = -1;
        if (card.type == CardTypes.ATAQUE && w == "S") value = strength;
        else if (card.type == CardTypes.DEFESA && w == "D") value = defense;
        else if ((card.type == CardTypes.DEFESA || card.type == CardTypes.ITEM) && w == "C") value = cure;
        else if (card.type == CardTypes.ITEM && w == "D") value = defense;
        else if (card.type == CardTypes.BOOST && w == "X") value = cardDraw;

        if (value >= 0)
        {
            written = value.ToString();
            return "Numero_" + value;
        }

        if (int.TryParse(raw, out value)) return "Numero_" + value;
        return Signs != null ? Signs.Resolve(raw) : null;
    }

    // Renders one word at whatever stage the player has reached with it: the
    // written word or its sign(s). spriteCount says how many signs it drew.
    private string StagedInline(string signWord, string plainText, out int spriteCount)
    {
        spriteCount = 0;
        string[] names = Signs != null ? Signs.SpriteNames(signWord) : null;
        if (names == null || names.Length == 0 || !InlineSpritesExist(names)) return plainText;
        if (SignProgression.GetStage(signWord) != SignProgression.Stage.Sign) return plainText;

        // The TMP atlas is a white mask, unlike the standalone sign sprites which
        // are authored in colour - so inline sprites need tint=1 to pick up the
        // surrounding text colour, or they render white. Inline sprites render at
        // the font size, and SignWriting glyphs are dense enough that at
        // description size they collapse into a smudge -- hence the size bump.
        var sb = new System.Text.StringBuilder();
        sb.Append("<size=").Append(Mathf.RoundToInt(inlineSignScale * 100f)).Append("%>");
        foreach (var n in names) sb.Append("<sprite name=\"").Append(n).Append("\" tint=1>");
        sb.Append("</size>");
        spriteCount = names.Length;
        return sb.ToString();
    }

    // A sign added since the TMP sprite asset was last rebuilt has a sprite
    // but no inline glyph; it stays written out rather than drawing a blank.
    private static bool InlineSpritesExist(string[] names)
    {
        var sa = TMP_Settings.defaultSpriteAsset;
        if (sa == null) return false;
        foreach (var n in names)
            if (sa.GetSpriteIndexFromName(n) < 0) return false;
        return true;
    }

    // Same staging, but sized so the sign fits a small badge no matter how wide
    // its glyph is: SignWriting glyphs vary a lot in aspect, and at a fixed
    // <size> the wide ones spill out of the circle. Here the size is derived from
    // the glyph's own width/height so every badge sign lands at about the same
    // on-screen width.
    [Range(0.6f, 2f)] public float badgeSignWidthEm = 1.15f;

    private string StagedInlineBadge(string signWord, string plainText)
    {
        bool hasSign = Signs != null && Signs.Has(signWord) && InlineSpritesExist(new[] { signWord });
        if (!hasSign || SignProgression.GetStage(signWord) != SignProgression.Stage.Sign)
            return plainText;

        float aspect = Mathf.Max(0.25f, SignGlyphAspect(signWord));
        int pct = Mathf.Clamp(Mathf.RoundToInt(badgeSignWidthEm * 100f / aspect), 45, 170);
        return "<size=" + pct + "%><sprite name=\"" + signWord + "\" tint=1></size>";
    }

    // width / height of a sign glyph in the active TMP sprite asset.
    private static float SignGlyphAspect(string signWord)
    {
        var sa = TMPro.TMP_Settings.defaultSpriteAsset;
        if (sa == null) return 1f;
        var ch = sa.spriteCharacterTable.Find(c => c.name == signWord);
        if (ch == null || ch.glyph == null || ch.glyph.glyphRect.height <= 0) return 1f;
        return (float)ch.glyph.glyphRect.width / ch.glyph.glyphRect.height;
    }

    // What each inline sign in the description replaced, in order, so the help
    // can say the exact word the sentence used ("dano", not "Dano") and look up
    // the whole word even when it is drawn as several signs.
    private struct DescSign { public string written; public string key; }
    private readonly List<DescSign> _descSigns = new List<DescSign>();

    private string ProcessDescription(string originalDesc)
    {
        _descSigns.Clear();
        if (string.IsNullOrEmpty(originalDesc)) return "";

        string[] temp = originalDesc.Split(' ');
        var sb = new System.Text.StringBuilder();

        for (int i = 0; i < temp.Length; i++)
        {
            string written;
            string key = DescriptionWordKey(temp[i], out written);

            int sprites = 0;
            string shown = key != null ? StagedInline(key, written, out sprites) : written;
            for (int k = 0; k < sprites; k++) _descSigns.Add(new DescSign { written = written, key = key });

            sb.Append(shown);
            if (i < temp.Length - 1) sb.Append(' ');
        }

        return sb.ToString();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (CardManager.instance == null) return;

        // Ajuda is open on this card: the click picks a word to translate
        // instead of playing the card.
        if (CardHelp.instance != null && CardHelp.instance.HandleCardClick(this, eventData)) return;

        // Raised already, so this click plays it - no double-click window.
        // Requiring clickCount >= 2 meant a second click that landed a moment
        // too late fell through to SelectCard, which toggled the card back
        // down; it read as the card deselecting itself and refusing to play
        // until you clicked several more times.
        if (IsSelected)
        {
            UseCard();
            return;
        }

        // First click raises the card so it can be read. Clicking anywhere off
        // the card puts it back down - that is DeselectClickCatcher's job.
        CardManager.instance.SelectCard(this);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        IsHovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        IsHovered = false;
    }

    // Set the frame a card is actually played, read (and cleared) by
    // CardManager.DiscardCard so it knows to burn the card away rather than
    // slide it off quietly.
    private bool _played;
    public bool ConsumePlayedFlag()
    {
        bool v = _played;
        _played = false;
        return v;
    }

    public void UseCard()
    {
        if (CardManager.instance != null && CardManager.instance.selectedCard == this)
        {
            CardManager.instance.selectedCard = null;
        }

        // Playing a card is what teaches its signs, so this is what advances the
        // text -> sign progression for the words on this particular card.

        // ... (Keep your existing combat/usage logic exactly as it was)
        if (GameManager.instance.currentState == GameStates.ENDMATCH)
        {
            StartCoroutine(CardManager.instance.ResetCombatWithNewDeck());
            CardManager.instance.currentAvailableCards.Add(card);
        }
        if (GameManager.instance.currentState == GameStates.COMBAT)
        {
            if (CardManager.instance.CanUseCard(this))
            {
                // The burn-away (CardManager.DiscardCard -> CardBurnAway) is the
                // play effect now; the old particle burst sat on top of it.
                _played = true;   // tells DiscardCard to burn this card away
                CardManager.instance.currentStamina -= cardStamina;
                
                // The type picks the card's headline effect and its sound. Draw and
                // cure are applied from the values themselves, exactly like the action
                // badges are, so a card that does two things does both regardless of
                // which type it is filed under - Furacao is an ATAQUE that also draws,
                // and that draw used to be silently dropped here.
                switch (cardType)
                {
                    case CardTypes.ATAQUE:
                        CombatManager.instance.Attack(strength, this);
                        _audioSource.PlayOneShot(_attackSFX, GameSettings.SfxVolume);
                        break;
                    case CardTypes.DEFESA:
                        CombatManager.instance.AddDefense(defense, this);
                        _audioSource.PlayOneShot(_defenseSFX, GameSettings.SfxVolume);
                        break;
                    case CardTypes.ITEM:
                        _audioSource.PlayOneShot(_itemSFX, GameSettings.SfxVolume);
                        break;
                    case CardTypes.BOOST:
                        break;
                }

                if (cure > 0) CombatManager.instance.Heal(cure, this);
                if (cardDraw > 0) StartCoroutine(CardManager.instance.DrawCards(this));

                UIManager.instance.UpdateDisplay();
            }
        }
    }
}
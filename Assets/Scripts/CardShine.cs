using UnityEngine;
using UnityEngine.UI;

// The halo around a selected card. Two rings, both generated procedurally and
// 9-sliced so the glow stays the same thickness at any card size or scale:
//
//   inner - a tight white rim hugging the card border (always white)
//   outer - a wider coloured bloom that pulses; its hue is fixed by the card's
//           type: red for Ataque, blue for Defesa, green for Cura/Item.
//
// The corners are rounded to follow the card's own rounded silhouette; a square
// ring read as a box floating behind a rounded card.
[RequireComponent(typeof(RectTransform))]
public class CardShine : MonoBehaviour
{
    [Header("Pulse")]
    [SerializeField] private float pulseSpeed = 1.6f;
    [SerializeField] private float minAlpha = 0.25f;
    [SerializeField] private float maxAlpha = 0.85f;

    [Header("Outer bloom by card type")]
    [SerializeField] private Color attackColor  = new Color(0.95f, 0.12f, 0.12f, 1f); // Ataque - red
    [SerializeField] private Color defenseColor = new Color(0.22f, 0.55f, 1.00f, 1f); // Defesa - blue
    [SerializeField] private Color cureColor    = new Color(0.20f, 0.90f, 0.38f, 1f); // Cura   - green
    [SerializeField] private Color boostColor   = new Color(1.00f, 0.82f, 0.35f, 1f); // Boost  - gold
    [SerializeField] private Color fallbackColor = new Color(1f, 0.85f, 0.45f, 1f);

    [Header("Inner rim")]
    [SerializeField] private Color innerColor = Color.white;
    [Range(0f, 1f)] [SerializeField] private float innerStrength = 1f;
    [SerializeField] private float innerInset = 7f;

    private static Sprite _outerSprite;
    private static Sprite _innerSprite;

    private Image _outer;
    private Image _inner;
    private float _timer;

    // Resolved from the owning card's type; defaults until SetType / OnEnable runs.
    private Color _bloom;

    private void Awake()
    {
        _outer = GetComponent<Image>();
        _outer.sprite = GetRing(ref _outerSprite, "CardShineOuter", 20f, 6f);
        _outer.type = Image.Type.Sliced;
        _outer.raycastTarget = false;

        BuildInner();
        _bloom = fallbackColor;
        _timer = Random.Range(0f, 10f);   // desync cards in the same hand
    }

    private void OnEnable()
    {
        // Shine objects start disabled and are switched on only for the selected
        // card, so resolving the colour here keeps it correct even if the card's
        // data was assigned after Awake.
        var owner = GetComponentInParent<CardDisplay>();
        if (owner != null) SetType(owner.cardType);
    }

    // Called by CardDisplay once the card's data is known, and again from OnEnable.
    public void SetType(CardTypes type)
    {
        _bloom = ColorFor(type);
    }

    /// The card colour for a type. This is the one place the per-type palette
    /// lives, so the border outline can tint itself from the same source rather
    /// than keeping a second copy that drifts out of sync.
    /// Safe to call on a disabled Shine - it only reads serialised fields.
    public Color ColorFor(CardTypes type)
    {
        switch (type)
        {
            case CardTypes.ATAQUE: return attackColor;
            case CardTypes.DEFESA: return defenseColor;
            case CardTypes.ITEM:   return cureColor;
            case CardTypes.BOOST:  return boostColor;
            default:               return fallbackColor;
        }
    }

    // The white rim is its own Image so it can sit at a different inset and keep
    // a constant colour while the bloom behind it pulses.
    private void BuildInner()
    {
        var existing = transform.Find("InnerRim");
        if (existing != null) _inner = existing.GetComponent<Image>();

        if (_inner == null)
        {
            var go = new GameObject("InnerRim", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _inner = go.AddComponent<Image>();
            _inner.raycastTarget = false;
        }

        var rt = _inner.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(innerInset, innerInset);
        rt.offsetMax = new Vector2(-innerInset, -innerInset);

        _inner.sprite = GetRing(ref _innerSprite, "CardShineInner", 12f, 3.5f);
        _inner.type = Image.Type.Sliced;
    }

    private void Update()
    {
        _timer += Time.unscaledDeltaTime;

        float alphaT = (Mathf.Sin(_timer * pulseSpeed) + 1f) * 0.5f;
        float alpha = Mathf.Lerp(minAlpha, maxAlpha, alphaT);

        Color c = _bloom;
        c.a = alpha;
        _outer.color = c;

        if (_inner != null)
        {
            Color i = innerColor;
            i.a = alpha * innerStrength;
            _inner.color = i;
        }
    }

    // A rounded rectangular ring: opaque along the border, fading out both
    // outward and inward, with the corners following a rounded silhouette.
    private static Sprite GetRing(ref Sprite cache, string name, float borderPx, float peak)
    {
        if (cache != null) return cache;

        const int size = 96;
        const float corner = 26f;   // radius of the rounded silhouette
        int b = Mathf.RoundToInt(borderPx);

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.name = name;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Signed distance to a rounded rectangle: straight along the
                // edges, circular within `corner` of each corner.
                float dx = Mathf.Abs(x + 0.5f - size * 0.5f) - (size * 0.5f - corner);
                float dy = Mathf.Abs(y + 0.5f - size * 0.5f) - (size * 0.5f - corner);
                float outside = Mathf.Sqrt(Mathf.Max(dx, 0f) * Mathf.Max(dx, 0f) +
                                           Mathf.Max(dy, 0f) * Mathf.Max(dy, 0f));
                float dist = corner - (outside + Mathf.Min(Mathf.Max(dx, dy), 0f));

                float a;
                if (dist < 0f) a = 0f;                       // outside the silhouette
                else if (dist < peak) a = dist / peak;       // feather in from the outer edge
                else a = 1f - Mathf.Clamp01((dist - peak) / Mathf.Max(b - peak, 0.001f));

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
            }
        }
        tex.Apply();

        cache = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                              SpriteMeshType.FullRect, new Vector4(b + peak, b + peak, b + peak, b + peak));
        cache.name = name;
        return cache;
    }
}

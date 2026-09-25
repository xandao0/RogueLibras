using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Lays out one or more SignWriting sign sprites in a row, so a word that needs
// several signs (e.g. "carta comum" is two) renders as several glyphs side by
// side. Images are pooled, so swapping words doesn't churn allocations.
//
// It reports itself as a layout element, which is what keeps a half-learned card
// readable: the row sits in a HorizontalLayoutGroup next to its written word and
// the group gives each of them their own space. The old version tried to nudge
// the row sideways over a text field that occupied the same rect, and the two
// drew on top of each other.
[RequireComponent(typeof(RectTransform))]
public class SignRow : MonoBehaviour, ILayoutElement
{
    [Tooltip("Height of each sign in UI units. Width follows the sign's aspect.")]
    [SerializeField] private float signHeight = 44f;

    [Tooltip("Gap between signs in a multi-sign word.")]
    [SerializeField] private float spacing = 6f;

    [SerializeField] private Color tint = Color.white;

    [Header("Legibility")]
    [Tooltip("Material using RogueLibras/UI Sign Outline. SignWriting is fine " +
             "line art and collapses into a smudge at card size, so the glyph is " +
             "fattened and ringed. Leave empty to draw the signs plain.")]
    [SerializeField] private Material signMaterial;
    [Tooltip("How many source texels to thicken the strokes by.")]
    [SerializeField] private float bold = 2f;
    [Tooltip("Contrasting ring around the thickened glyph, in source texels.")]
    [SerializeField] private float outlineWidth = 2.5f;
    [SerializeField] private Color outlineColor = Color.white;
    [Tooltip("The sign SVGs are authored in colour. 0 keeps that colour; 1 " +
             "repaints them in the tint above. Multiplying a coloured sign by " +
             "a tint only ever darkens it, which is rarely what you want.")]
    [Range(0f, 1f)] [SerializeField] private float colorize = 0f;

    private Material _matInstance;

    private readonly List<Image> _pool = new List<Image>();
    private RectTransform _rt;
    private int _activeCount;

    /// Total width of the signs currently laid out.
    public float MeasuredWidth { get; private set; }

    private RectTransform Rt
    {
        get { if (_rt == null) _rt = (RectTransform)transform; return _rt; }
    }

    public void SetSigns(Sprite[] signs)
    {
        int count = signs != null ? signs.Length : 0;

        while (_pool.Count < count)
        {
            var go = new GameObject("Sign" + _pool.Count, typeof(RectTransform));
            go.transform.SetParent(Rt, false);
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            img.material = SignMaterial();
            _pool.Add(img);
        }

        for (int i = 0; i < _pool.Count; i++)
        {
            bool used = i < count;
            _pool[i].gameObject.SetActive(used);
            if (!used) continue;

            _pool[i].sprite = signs[i];
            _pool[i].color = tint;
        }

        _activeCount = count;
        Layout(count);

        // Our preferred width just changed, so whoever is positioning us needs to
        // measure again.
        var parent = transform.parent as RectTransform;
        if (parent != null) LayoutRebuilder.MarkLayoutForRebuild(parent);
    }

    public void Clear()
    {
        SetSigns(null);
    }

    /// One instance per row, so each row can carry its own outline colour -
    /// UI Images do not support MaterialPropertyBlocks, so a shared asset
    /// cannot be varied per row.
    private Material SignMaterial()
    {
        if (_matInstance != null) return _matInstance;

        Shader shader = signMaterial != null
            ? signMaterial.shader
            : Shader.Find("RogueLibras/UI Sign Outline");
        if (shader == null) return null;

        _matInstance = signMaterial != null
            ? new Material(signMaterial)
            : new Material(shader);
        _matInstance.name = name + " (Signs)";
        _matInstance.SetFloat("_Bold", bold);
        _matInstance.SetFloat("_OutlineWidth", outlineWidth);
        _matInstance.SetColor("_OutlineColor", outlineColor);
        _matInstance.SetFloat("_Colorize", colorize);
        return _matInstance;
    }

    private void OnDestroy()
    {
        if (_matInstance != null) Destroy(_matInstance);
    }

    private void Layout(int count)
    {
        if (count == 0) { MeasuredWidth = 0f; return; }

        // Width of each sign from its own aspect, then centre the whole row.
        var widths = new float[count];
        float total = 0f;
        for (int i = 0; i < count; i++)
        {
            Sprite s = _pool[i].sprite;
            float aspect = (s != null && s.rect.height > 0f) ? s.rect.width / s.rect.height : 1f;
            widths[i] = signHeight * aspect;
            total += widths[i];
        }
        total += spacing * (count - 1);
        MeasuredWidth = total;

        float x = -total * 0.5f;
        for (int i = 0; i < count; i++)
        {
            var rt = _pool[i].rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(widths[i], signHeight);
            rt.anchoredPosition = new Vector2(x, 0f);
            x += widths[i] + spacing;
        }
    }

    // --- ILayoutElement: an empty row takes no space at all, so a card that has
    // not learned its word yet lays out exactly as if the row weren't there. ---
    public float minWidth { get { return 0f; } }
    public float preferredWidth { get { return MeasuredWidth; } }
    public float flexibleWidth { get { return -1f; } }
    public float minHeight { get { return 0f; } }
    public float preferredHeight { get { return _activeCount > 0 ? signHeight : 0f; } }
    public float flexibleHeight { get { return -1f; } }
    public int layoutPriority { get { return 1; } }
    public void CalculateLayoutInputHorizontal() { }
    public void CalculateLayoutInputVertical() { }
}

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The discard pile is always lit: a small flame licks over it at rest, flares to
// a medium burst on any discard, and to a big burst when a card was deliberately
// played. The counter punches in time with the burst.
//   - Play()      : ordinary discard (end of turn, draw-then-discard, etc.)
//   - PlayBig()   : a card the player actively used
public class DiscardBurnEffect : MonoBehaviour
{
    [Header("Flame material")]
    [Tooltip("Material using the RogueLibras/UI Fire shader.")]
    [SerializeField] private Material fireMaterial;

    [Header("Idle flame (always on)")]
    [SerializeField] private Vector2 idleSize = new Vector2(46f, 60f);
    [SerializeField] private Vector2 idleOffset = new Vector2(0f, -46f);
    [Range(0f, 1f)] [SerializeField] private float idleAlpha = 0.55f;
    [SerializeField] private float idleFlicker = 0.12f;

    [Header("Burst flame")]
    [SerializeField] private float flameDuration = 1.1f;
    [SerializeField] private Vector2 flameSize = new Vector2(95f, 140f);
    [SerializeField] private Vector2 flameOffset = new Vector2(0f, -55f);
    [Tooltip("Multiplier applied to size/duration when a card was actively played.")]
    [SerializeField] private float bigMultiplier = 1.8f;

    [Header("Counter punch")]
    [SerializeField] private float punchScale = 1.4f;
    [SerializeField] private float punchDuration = 0.35f;

    [Header("Pile stack")]
    [Tooltip("Duplicate the pile art behind itself and tilt it, so the discard " +
             "reads as a stack of cards rather than one lonely card.")]
    [SerializeField] private bool buildBackCard = true;
    [SerializeField] private float backCardAngle = 13f;
    [SerializeField] private Vector2 backCardOffset = new Vector2(-9f, -6f);
    [Tooltip("The card underneath sits in the shadow of the one on top.")]
    [SerializeField] private Color backCardTint = new Color(0.66f, 0.60f, 0.56f, 1f);

    [Header("Burn away the top card")]
    [Tooltip("Material using RogueLibras/UI Dissolve. Left empty, the shader is " +
             "looked up by name.")]
    [SerializeField] private Material dissolveMaterial;
    [SerializeField] private float dissolveDuration = 0.8f;

    private Image _pileImage;          // the card on top - the one that burns
    private Image _dissolveImage;      // its stand-in while it is burning
    private Material _dissolveInstance;

    private Image _flameImage;
    private RectTransform _flameRt;
    private Image _idleImage;
    private RectTransform _idleRt;
    private TMP_Text _countText;
    private Vector3 _countBaseScale = Vector3.one;
    private float _idleTimer;

    private void Awake()
    {
        _pileImage = GetComponent<Image>();
        BuildBackCard();
        BuildDissolveLayer();

        _idleRt = MakeFlame("IdleFlame", idleSize, idleOffset, out _idleImage);
        _idleImage.color = new Color(1f, 1f, 1f, idleAlpha);
        _idleRt.localScale = Vector3.one;

        _flameRt = MakeFlame("BurnFlame", flameSize, flameOffset, out _flameImage);
        _flameImage.color = new Color(1f, 1f, 1f, 0f);
        _flameRt.localScale = Vector3.zero;

        var texts = GetComponentsInChildren<TMP_Text>(true);
        foreach (var t in texts)
            if (t.gameObject.name == "Count") { _countText = t; break; }
        if (_countText == null && texts.Length > 0) _countText = texts[0];
        if (_countText != null) _countBaseScale = _countText.rectTransform.localScale;

        // Flames over the card, count over the flames.
        if (_dissolveImage != null) _dissolveImage.transform.SetAsFirstSibling();
        if (_idleRt != null) _idleRt.SetSiblingIndex(_dissolveImage != null ? 1 : 0);
        if (_flameRt != null) _flameRt.SetSiblingIndex(_idleRt.GetSiblingIndex() + 1);
        if (_countText != null) _countText.transform.SetAsLastSibling();

        _idleTimer = Random.Range(0f, 10f);
    }

    private void OnDestroy()
    {
        if (_dissolveInstance != null) Destroy(_dissolveInstance);
    }

    /// A second copy of the pile art, parented alongside this one and ordered
    /// before it so it draws underneath. It has to be a sibling rather than a
    /// child: uGUI always paints children over their parent's own graphic.
    private void BuildBackCard()
    {
        if (!buildBackCard || _pileImage == null || transform.parent == null) return;

        var existing = transform.parent.Find(name + "Back");
        GameObject go = existing != null
            ? existing.gameObject
            : new GameObject(name + "Back", typeof(RectTransform));

        var self = (RectTransform)transform;
        var rt = (RectTransform)go.transform;
        rt.SetParent(transform.parent, false);
        rt.SetSiblingIndex(self.GetSiblingIndex());   // immediately behind
        rt.anchorMin = self.anchorMin;
        rt.anchorMax = self.anchorMax;
        rt.pivot = self.pivot;
        rt.sizeDelta = self.sizeDelta;
        rt.anchoredPosition = self.anchoredPosition + backCardOffset;
        rt.localRotation = Quaternion.Euler(0f, 0f, backCardAngle);
        rt.localScale = self.localScale;

        var img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();
        img.sprite = _pileImage.sprite;
        img.type = _pileImage.type;
        img.preserveAspect = _pileImage.preserveAspect;
        img.color = backCardTint;
        img.raycastTarget = false;
    }

    /// Stand-in for the top card while it burns. The real one is switched off
    /// for the duration, otherwise it would show through the holes being eaten
    /// in this copy.
    private void BuildDissolveLayer()
    {
        if (_pileImage == null) return;

        var existing = transform.Find("BurningTop");
        GameObject go = existing != null
            ? existing.gameObject
            : new GameObject("BurningTop", typeof(RectTransform));
        go.transform.SetParent(transform, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _dissolveImage = go.GetComponent<Image>();
        if (_dissolveImage == null) _dissolveImage = go.AddComponent<Image>();
        _dissolveImage.sprite = _pileImage.sprite;
        _dissolveImage.type = _pileImage.type;
        _dissolveImage.preserveAspect = _pileImage.preserveAspect;
        _dissolveImage.color = _pileImage.color;
        _dissolveImage.raycastTarget = false;

        Shader shader = dissolveMaterial != null
            ? dissolveMaterial.shader
            : Shader.Find("RogueLibras/UI Dissolve");
        if (shader != null)
        {
            _dissolveInstance = dissolveMaterial != null
                ? new Material(dissolveMaterial)
                : new Material(shader);
            _dissolveInstance.name = name + " (Dissolve)";
            _dissolveImage.material = _dissolveInstance;
        }

        go.SetActive(false);
    }

    private RectTransform MakeFlame(string goName, Vector2 size, Vector2 offset, out Image img)
    {
        var existing = transform.Find(goName);
        GameObject go = existing != null ? existing.gameObject : new GameObject(goName, typeof(RectTransform));
        go.transform.SetParent(transform, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0f);       // grows upward from its base
        rt.anchoredPosition = offset;
        rt.sizeDelta = size;

        img = go.GetComponent<Image>();
        if (img == null) img = go.AddComponent<Image>();
        img.raycastTarget = false;
        img.sprite = null;                      // shader fills the whole quad
        if (fireMaterial != null) img.material = fireMaterial;
        return rt;
    }

    private void Update()
    {
        // Idle flame: gentle vertical flicker so the pile is never dead.
        _idleTimer += Time.unscaledDeltaTime;
        if (_idleImage != null)
        {
            float f = 1f + Mathf.Sin(_idleTimer * 7.3f) * idleFlicker
                         + Mathf.Sin(_idleTimer * 17.1f) * idleFlicker * 0.4f;
            _idleRt.localScale = new Vector3(1f, f, 1f);
            var c = _idleImage.color;
            c.a = idleAlpha * (0.85f + 0.15f * Mathf.Sin(_idleTimer * 9f));
            _idleImage.color = c;
        }
    }

    public void Play()      => Burst(1f);
    public void PlayBig()   => Burst(bigMultiplier);

    private void Burst(float scale)
    {
        StopAllCoroutines();
        ResetDissolve();                       // a burst can interrupt a burst
        StartCoroutine(FlameRoutine(scale));
        StartCoroutine(PunchRoutine(scale));
        StartCoroutine(DissolveRoutine(scale));
        // keep the idle flicker alive during the burst
        StartCoroutine(ResumeIdle());
    }

    private void ResetDissolve()
    {
        if (_dissolveInstance != null) _dissolveInstance.SetFloat("_Dissolve", 0f);
        if (_dissolveImage != null) _dissolveImage.gameObject.SetActive(false);
        if (_pileImage != null) _pileImage.enabled = true;
    }

    // The card on top burns away along a noisy front, uncovering the one behind
    // it, then the pile snaps back to full so it is ready for the next discard.
    private IEnumerator DissolveRoutine(float scale)
    {
        if (_dissolveImage == null || _dissolveInstance == null || _pileImage == null)
            yield break;

        _dissolveImage.sprite = _pileImage.sprite;   // follow any art swap
        _dissolveImage.gameObject.SetActive(true);
        _pileImage.enabled = false;

        float dur = dissolveDuration * Mathf.Lerp(1f, scale, 0.35f);
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            _dissolveInstance.SetFloat("_Dissolve", Mathf.Clamp01(t / dur));
            yield return null;
        }

        ResetDissolve();
    }

    private IEnumerator ResumeIdle()
    {
        yield return null; // Update() handles the idle flame every frame anyway
    }

    private IEnumerator FlameRoutine(float scale)
    {
        _flameRt.sizeDelta = flameSize * Mathf.Lerp(1f, scale, 0.6f);
        float dur = flameDuration * Mathf.Lerp(1f, scale, 0.5f);
        float t = 0f;

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / dur);

            float envelope = p < 0.22f
                ? Mathf.SmoothStep(0f, 1f, p / 0.22f)
                : Mathf.SmoothStep(1f, 0f, (p - 0.22f) / 0.78f);

            float grow = Mathf.Lerp(0.8f, 1.15f, p) * scale;
            _flameRt.localScale = new Vector3(envelope * grow, envelope * grow, 1f);

            var c = _flameImage.color;
            c.a = envelope;
            _flameImage.color = c;
            yield return null;
        }

        _flameRt.localScale = Vector3.zero;
        var done = _flameImage.color; done.a = 0f;
        _flameImage.color = done;
    }

    private IEnumerator PunchRoutine(float scale)
    {
        if (_countText == null) yield break;

        var rt = _countText.rectTransform;
        float amount = (punchScale - 1f) * Mathf.Lerp(1f, scale, 0.6f);
        float dur = punchDuration;
        float t = 0f;

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / dur);
            float s = 1f + Mathf.Sin(p * Mathf.PI) * amount;
            rt.localScale = _countBaseScale * s;
            yield return null;
        }
        rt.localScale = _countBaseScale;
    }
}

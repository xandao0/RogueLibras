using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The player's block ("Bloqueio"), shown as a light bar in the style of the
// enemy health bar: a skinned Slider, a shield icon and the value.
//
// The value is a number or its SignWriting sign (still white), following the
// same one-word-per-turn progression as the cards - see SignProgression.
public class BlockedBar : MonoBehaviour, ISignWordSource
{
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_Text amountText;

    [Tooltip("Fill reference. The bar is full at this value; larger values push " +
             "the reference up so the fill stays proportional without overflowing.")]
    [SerializeField] private float softMax = 20f;

    [Tooltip("Size of the sign that replaces the number, relative to the text.")]
    [Range(0.6f, 3f)] [SerializeField] private float signScale = 1.5f;

    [Tooltip("Fade the whole bar down while block is zero instead of hiding it.")]
    [Range(0f, 1f)] [SerializeField] private float emptyAlpha = 0.35f;
    [SerializeField] private float lerpSpeed = 10f;

    private CanvasGroup _cg;
    private int _value;
    private float _targetFill;
    private float _targetAlpha = 1f;

    private void Reset()      { AutoWire(); }
    private void OnValidate() { if (slider == null || amountText == null) AutoWire(); }

    private void Awake()
    {
        AutoWire();
        _cg = GetComponent<CanvasGroup>();
        if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();
        _cg.interactable = false;
        _cg.blocksRaycasts = false;

        if (amountText != null) amountText.color = Color.white;
        SignText.UsePlainSigns(amountText);   // the card outline smears signs this size
    }

    private void AutoWire()
    {
        if (slider == null) slider = GetComponentInChildren<Slider>(true);
        if (amountText == null)
        {
            var t = transform.Find("BlockAmount");
            if (t != null) amountText = t.GetComponent<TMP_Text>();
        }
    }

    public void SetBlock(int current)
    {
        current = Mathf.Max(0, current);
        _value = current;

        if (amountText != null)
        {
            amountText.color = Color.white;
            amountText.text = SignText.Number(current, signScale);
        }

        float max = Mathf.Max(softMax, current);
        _targetFill = max > 0f ? current / max : 0f;
        _targetAlpha = current > 0 ? 1f : emptyAlpha;
    }

    public void CollectSignWords(System.Collections.Generic.List<string> into)
    {
        SignText.NumberWords(_value, into);
    }

    public void RefreshSignText()
    {
        if (amountText != null) amountText.text = SignText.Number(_value, signScale);
    }

    private void Update()
    {
        float t = 1f - Mathf.Exp(-lerpSpeed * Time.unscaledDeltaTime);
        if (slider != null) slider.value = Mathf.Lerp(slider.value, _targetFill, t);
        if (_cg != null)    _cg.alpha    = Mathf.Lerp(_cg.alpha, _targetAlpha, t);
    }
}

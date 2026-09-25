using UnityEngine;
using UnityEngine.UI;
using TMPro;

// The player's block ("Bloqueio"), shown as a light bar in the style of the
// enemy health bar: a skinned Slider, a shield icon and the value.
//
// The value reads as a plain white number until the player has been through a
// few turns, then it switches to the SignWriting glyph for that number (still
// white) - the same "learn the sign in context" idea the cards use.
public class BlockedBar : MonoBehaviour
{
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_Text amountText;

    [Tooltip("Fill reference. The bar is full at this value; larger values push " +
             "the reference up so the fill stays proportional without overflowing.")]
    [SerializeField] private float softMax = 20f;

    [Tooltip("Turns played before the number is shown as a SignWriting sign.")]
    [SerializeField] private int signAfterTurn = 5;

    [Tooltip("Fade the whole bar down while block is zero instead of hiding it.")]
    [Range(0f, 1f)] [SerializeField] private float emptyAlpha = 0.35f;
    [SerializeField] private float lerpSpeed = 10f;

    private CanvasGroup _cg;
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

        if (amountText != null)
        {
            amountText.color = Color.white;
            amountText.text = AsSignOrNumber(current);
        }

        float max = Mathf.Max(softMax, current);
        _targetFill = max > 0f ? current / max : 0f;
        _targetAlpha = current > 0 ? 1f : emptyAlpha;
    }

    private string AsSignOrNumber(int value)
    {
        if (SignProgression.Turn >= signAfterTurn && HasNumeroSign(value))
            return "<size=150%><sprite name=\"Numero_" + value + "\" tint=1></size>";
        return value.ToString();
    }

    private static bool HasNumeroSign(int value)
    {
        var sa = TMP_Settings.defaultSpriteAsset;
        if (sa == null) return false;
        string name = "Numero_" + value;
        return sa.spriteCharacterTable.Exists(c => c.name == name);
    }

    private void Update()
    {
        float t = 1f - Mathf.Exp(-lerpSpeed * Time.unscaledDeltaTime);
        if (slider != null) slider.value = Mathf.Lerp(slider.value, _targetFill, t);
        if (_cg != null)    _cg.alpha    = Mathf.Lerp(_cg.alpha, _targetAlpha, t);
    }
}

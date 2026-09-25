using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Standard hover/press feedback for the HUD buttons (Opções, Sair, Encerrar
// Turno). Idle they render normally; on hover a RogueLibras/UI Brighten material
// pushes them overbright, and they dip slightly while held. One component + one
// shared shader across every button so the set stays consistent - and so the art
// can later be swapped for a finished image without touching this logic.
[RequireComponent(typeof(Graphic))]
public class ButtonHover : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private float idleBrightness    = 1.0f;
    [SerializeField] private float hoverBrightness   = 1.35f;
    [SerializeField] private float pressedBrightness = 0.9f;
    [SerializeField] private float lerpSpeed         = 14f;

    [Tooltip("Optional slight scale bump on hover. 1 = no scale.")]
    [SerializeField] private float hoverScale = 1.03f;

    private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");

    private Graphic _graphic;
    private Material _matInstance;
    private Color _baseColor;
    private Vector3 _baseScale;
    private bool _hovered;
    private bool _pressed;
    private float _current;

    private void Awake()
    {
        _graphic = GetComponent<Graphic>();
        _baseScale = transform.localScale;
        _baseColor = _graphic.color;
        _current = idleBrightness;

        var shader = Shader.Find("RogueLibras/UI Brighten");
        if (shader != null)
        {
            _matInstance = new Material(shader) { name = name + " (Brighten)" };
            // carry over whatever tint the graphic already had
            _matInstance.SetColor("_Color", Color.white);
            _graphic.material = _matInstance;
        }
        Apply(idleBrightness);
    }

    private void OnDestroy()
    {
        if (_matInstance != null) Destroy(_matInstance);
    }

    public void OnPointerEnter(PointerEventData e) => _hovered = true;
    public void OnPointerExit(PointerEventData e)  { _hovered = false; _pressed = false; }
    public void OnPointerDown(PointerEventData e)  => _pressed = true;
    public void OnPointerUp(PointerEventData e)    => _pressed = false;

    private void Update()
    {
        float target = _pressed ? pressedBrightness : (_hovered ? hoverBrightness : idleBrightness);
        float t = 1f - Mathf.Exp(-lerpSpeed * Time.unscaledDeltaTime);
        _current = Mathf.Lerp(_current, target, t);
        Apply(_current);

        float scaleTarget = (_hovered && !_pressed) ? hoverScale : 1f;
        transform.localScale = Vector3.Lerp(transform.localScale, _baseScale * scaleTarget, t);
    }

    private void Apply(float brightness)
    {
        if (_matInstance != null)
        {
            _matInstance.SetFloat(BrightnessId, brightness);
            return;
        }

        // Shader.Find only sees shaders the build actually included, so in a
        // player the material above can come back null and the button would
        // light up in the editor and do nothing once shipped. Tinting the
        // graphic is a weaker but always-available stand-in.
        _graphic.color = new Color(
            Mathf.Clamp01(_baseColor.r * brightness),
            Mathf.Clamp01(_baseColor.g * brightness),
            Mathf.Clamp01(_baseColor.b * brightness),
            _baseColor.a);
    }
}

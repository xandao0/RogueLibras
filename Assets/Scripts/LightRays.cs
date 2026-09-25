using UnityEngine;
using UnityEngine.UI;

// Drives the RogueLibras/Light Rays material on an Image: keeps the shader's
// aspect correction matched to the rect (so the fan stays circular however the
// quad is stretched) and breathes the intensity, which sells the rays as light
// through moving air rather than a decal.
//
// Runs on an instanced material so several ray Images can share one material
// asset without fighting over its properties. Play mode only on purpose: the
// only way to reach the material in edit mode is the shared asset itself, and
// writing to that every frame would leave the .mat permanently dirty.
[RequireComponent(typeof(Image))]
public class LightRays : MonoBehaviour
{
    [Tooltip("Average intensity. The pulse swings either side of this.")]
    [SerializeField] private float intensity = 1f;

    [Tooltip("How far the intensity swings, as a fraction of Intensity.")]
    [Range(0f, 1f)] [SerializeField] private float pulseAmount = 0.22f;

    [Tooltip("Pulse rate in cycles per second. Slow reads as air, fast reads as flicker.")]
    [SerializeField] private float pulseSpeed = 0.13f;

    private static readonly int AspectId = Shader.PropertyToID("_Aspect");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

    private Image _image;
    private RectTransform _rt;
    private Material _instance;
    private float _phase;

    private void OnEnable()
    {
        _image = GetComponent<Image>();
        _rt = (RectTransform)transform;
        _phase = Random.Range(0f, 100f);
        Apply();
    }

    private void OnDisable()
    {
        if (_instance != null) Destroy(_instance);
        _instance = null;
    }

    private void Update()
    {
        _phase += Time.unscaledDeltaTime;
        Apply();
    }

    private void Apply()
    {
        if (_image == null || _image.material == null) return;

        if (_instance == null)
        {
            _instance = new Material(_image.material);
            _instance.name = _image.material.name + " (instance)";
            _image.material = _instance;
        }

        Material m = _instance;
        if (!m.HasProperty(AspectId)) return;

        Rect r = _rt.rect;
        m.SetFloat(AspectId, r.height > 0.0001f ? r.width / r.height : 1f);

        float pulse = 1f + Mathf.Sin(_phase * pulseSpeed * Mathf.PI * 2f) * pulseAmount;
        m.SetFloat(IntensityId, Mathf.Max(0f, intensity * pulse));
    }
}

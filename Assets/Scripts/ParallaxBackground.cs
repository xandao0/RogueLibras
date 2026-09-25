using UnityEngine;

// Pointer-driven parallax for scene layers on an otherwise static camera. Each
// layer drifts from its starting position by its own maxOffset, so several
// layers with different offsets read as depth. Works with a single layer or multiple.
//
// The raw pointer is smoothed before it drives anything, the response is eased so 
// jitter near the screen centre goes nowhere,
// and every layer settles with a damped follow rather than a lerp.
// A slow idle sway keeps the scene moving slightly when the mouse is not moving.
//
// Handles both kinds of target:
//  - RectTransform (UI layers)     -> offsets anchoredPosition, in canvas units.
//  - plain Transform (world sprites) -> offsets world position, so heavily
//    scaled parents don't multiply the drift into orbit.
public class ParallaxBackground : MonoBehaviour
{
    [System.Serializable]
    public class Layer
    {
        public Transform target;

        [Tooltip("Maximum drift at full pointer deflection. Keep it inside the " +
                 "layer's overscan so its edges never come into view.")]
        public Vector2 maxOffset = new Vector2(30f, 10f);

        [Tooltip("Move against the pointer instead of with it. Far layers " +
                 "usually look best inverted.")]
        public bool invert;

        [Tooltip("Scales this layer's share of Follow Time. Above 1 the layer " +
                 "trails the others, which on its own reads as extra distance.")]
        [Range(0.25f, 3f)] public float lag = 1f;

        [HideInInspector] public Vector3 startPos;
        [HideInInspector] public bool isRect;
        [HideInInspector] public Vector3 current;
        [HideInInspector] public Vector3 velocity;
    }

    [SerializeField] private Layer[] layers;

    [Header("Feel")]
    [Tooltip("Roughly how long a layer takes to settle after the pointer stops. " +
             "Bigger = smoother and heavier.")]
    [SerializeField] private float followTime = 0.55f;

    [Tooltip("Smoothing applied to the raw pointer before it drives anything. " +
             "This is what takes the twitch out of frame-to-frame mouse noise.")]
    [SerializeField] private float inputSmoothTime = 0.16f;

    [Tooltip("Pointer deflection within this fraction of the centre is ignored, " +
             "so the scene stays put while you read it.")]
    [Range(0f, 0.4f)] [SerializeField] private float deadZone = 0.07f;

    [Tooltip("Eases the response curve so the drift builds gently instead of " +
             "tracking the pointer one-to-one.")]
    [SerializeField] private bool easeResponse = true;

    [Header("Idle Drift")]
    [Tooltip("Slow sway added on top of the pointer, as a fraction of maxOffset, " +
             "so the scene keeps moving when the mouse is still.")]
    [SerializeField] private Vector2 idleAmount = new Vector2(0.16f, 0.11f);

    [Tooltip("Cycles per second for the idle sway on each axis. Deliberately " +
             "unrelated numbers, so the two axes never resync into a loop.")]
    [SerializeField] private Vector2 idleSpeed = new Vector2(0.083f, 0.061f);

    private Vector2 _pointer;
    private Vector2 _pointerVelocity;
    private float _phase;

    private void Awake()
    {
        if (layers == null || layers.Length == 0)
        {
            layers = new[] { new Layer { target = transform } };
        }

        _phase = Random.Range(0f, 100f);

        for (int i = 0; i < layers.Length; i++)
        {
            Layer l = layers[i];
            if (l.target == null) continue;

            var rt = l.target as RectTransform;
            l.isRect = rt != null;
            l.startPos = l.isRect ? (Vector3)rt.anchoredPosition : l.target.position;
            l.current = l.startPos;
            l.velocity = Vector3.zero;
        }
    }

    private void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;
        if (dt <= 0f) return;

        _phase += dt;

        // Smooth the input itself, then shape it. Doing it in that order means a
        // pointer that snaps across the screen still produces a gradual drift.
        _pointer = Vector2.SmoothDamp(_pointer, ReadPointer(), ref _pointerVelocity,
                                      Mathf.Max(0.0001f, inputSmoothTime), Mathf.Infinity, dt);

        Vector2 shaped = new Vector2(Shape(_pointer.x), Shape(_pointer.y));
        shaped.x = Mathf.Clamp(shaped.x + Mathf.Sin(_phase * idleSpeed.x * Mathf.PI * 2f) * idleAmount.x, -1f, 1f);
        shaped.y = Mathf.Clamp(shaped.y + Mathf.Sin(_phase * idleSpeed.y * Mathf.PI * 2f + 2.1f) * idleAmount.y, -1f, 1f);

        for (int i = 0; i < layers.Length; i++)
        {
            Layer l = layers[i];
            if (l.target == null) continue;

            float sign = l.invert ? -1f : 1f;
            Vector3 desired = l.startPos + new Vector3(shaped.x * l.maxOffset.x * sign,
                                                       shaped.y * l.maxOffset.y * sign,
                                                       0f);

            float settle = Mathf.Max(0.0001f, followTime * Mathf.Max(0.01f, l.lag));
            l.current = Vector3.SmoothDamp(l.current, desired, ref l.velocity, settle, Mathf.Infinity, dt);

            if (l.isRect) ((RectTransform)l.target).anchoredPosition = l.current;
            else l.target.position = l.current;
        }
    }

    // Pointer position normalised to -1..1 around the screen centre.
    private static Vector2 ReadPointer()
    {
        if (Screen.width <= 0 || Screen.height <= 0) return Vector2.zero;

        Vector3 m = Input.mousePosition;

        // A pointer that has left the window leaves stale coordinates behind.
        // Treat that as centred so the scene eases home instead of sticking at
        // an extreme.
        if (m.x < 0f || m.y < 0f || m.x > Screen.width || m.y > Screen.height) return Vector2.zero;

        return new Vector2(Mathf.Clamp((m.x / Screen.width) * 2f - 1f, -1f, 1f),
                           Mathf.Clamp((m.y / Screen.height) * 2f - 1f, -1f, 1f));
    }

    // Dead zone, re-expanded to the full range, then optionally smoothstepped.
    private float Shape(float v)
    {
        float sign = Mathf.Sign(v);
        float a = Mathf.Abs(v);

        if (a <= deadZone) return 0f;
        a = (a - deadZone) / Mathf.Max(0.0001f, 1f - deadZone);

        if (easeResponse) a = a * a * (3f - 2f * a);

        return sign * a;
    }
}

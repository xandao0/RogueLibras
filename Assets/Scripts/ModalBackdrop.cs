using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Blurs and dims whatever is already on screen behind a modal panel, the way a
/// dialog pushes the page behind it out of focus.
///
/// Every canvas in this project is Screen Space - Overlay, and overlay canvases
/// are composited after the camera has finished drawing - so a GrabPass shader
/// cannot see them, and rendering the camera to a texture would miss the whole
/// HUD. This grabs the finished frame instead, blurs it down, and parks it
/// behind the panel as a still image.
///
/// The panel's own content is hidden for the one frame being grabbed, otherwise
/// the modal would appear inside its own backdrop.
/// </summary>
[RequireComponent(typeof(Canvas))]
public class ModalBackdrop : MonoBehaviour
{
    public enum FlipMode { Auto, Never, Always }

    [Tooltip("The modal's content. Hidden for a single frame while the screen " +
             "behind it is grabbed, then switched straight back on.")]
    [SerializeField] private RectTransform content;

    [Header("Blur")]
    [Tooltip("The grab is scaled down by this much before blurring. Higher is " +
             "softer and cheaper, because the downscale blurs too.")]
    [Range(1, 8)] [SerializeField] private int downsample = 4;
    [Tooltip("Horizontal + vertical blur pairs. Each one widens the blur.")]
    [Range(0, 6)] [SerializeField] private int iterations = 3;
    [SerializeField] private float blurSpread = 1.35f;

    [Tooltip("Tint over the blurred grab. Darken here, or leave white and let " +
             "the panel's own dark image do it.")]
    [SerializeField] private Color tint = Color.white;

    [Tooltip("Grabs come out upside down on some graphics APIs. Auto follows " +
             "the API; flip it by hand only if the backdrop renders inverted.")]
    [SerializeField] private FlipMode flip = FlipMode.Auto;

    private RawImage _backdrop;
    private RenderTexture _blurred;
    private Material _blurMat;

    private void OnEnable()
    {
        StartCoroutine(Grab());
    }

    private void OnDisable()
    {
        // The panel is usually destroyed and rebuilt between matches, so hanging
        // on to a screen-sized target would leak one per opening.
        if (_backdrop != null) _backdrop.enabled = false;
        Release();
        if (content != null) content.gameObject.SetActive(true);
    }

    private void OnDestroy()
    {
        Release();
        if (_blurMat != null) Destroy(_blurMat);
    }

    private void Release()
    {
        if (_blurred == null) return;
        if (_backdrop != null) _backdrop.texture = null;
        _blurred.Release();
        Destroy(_blurred);
        _blurred = null;
    }

    private IEnumerator Grab()
    {
        EnsureBackdrop();
        _backdrop.enabled = false;
        if (content != null) content.gameObject.SetActive(false);

        // The grab has to happen after everything - including every overlay
        // canvas - has been composited for this frame.
        yield return new WaitForEndOfFrame();

        int w = Mathf.Max(1, Screen.width);
        int h = Mathf.Max(1, Screen.height);

        var full = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
        ScreenCapture.CaptureScreenshotIntoRenderTexture(full);

        int bw = Mathf.Max(1, w / downsample);
        int bh = Mathf.Max(1, h / downsample);

        Release();
        _blurred = new RenderTexture(bw, bh, 0, RenderTextureFormat.ARGB32)
        {
            name = name + " Backdrop",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        if (_blurMat == null)
        {
            var shader = Shader.Find("RogueLibras/Blit Blur");
            if (shader != null) _blurMat = new Material(shader) { name = "Modal Blur" };
        }

        if (_blurMat == null)
        {
            // No blur shader available - the downscale alone still softens it,
            // which is better than showing nothing.
            Graphics.Blit(full, _blurred);
        }
        else
        {
            var a = RenderTexture.GetTemporary(bw, bh, 0, RenderTextureFormat.ARGB32);
            var b = RenderTexture.GetTemporary(bw, bh, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(full, a);

            for (int i = 0; i < iterations; i++)
            {
                float spread = blurSpread * (1f + i);   // widen as we go
                _blurMat.SetVector("_BlurDir", new Vector4(spread, 0f, 0f, 0f));
                Graphics.Blit(a, b, _blurMat);
                _blurMat.SetVector("_BlurDir", new Vector4(0f, spread, 0f, 0f));
                Graphics.Blit(b, a, _blurMat);
            }

            Graphics.Blit(a, _blurred);
            RenderTexture.ReleaseTemporary(a);
            RenderTexture.ReleaseTemporary(b);
        }

        RenderTexture.ReleaseTemporary(full);

        _backdrop.texture = _blurred;
        _backdrop.color = tint;
        _backdrop.uvRect = ShouldFlip() ? new Rect(0f, 1f, 1f, -1f) : new Rect(0f, 0f, 1f, 1f);
        _backdrop.enabled = true;

        if (content != null) content.gameObject.SetActive(true);
    }

    private bool ShouldFlip()
    {
        switch (flip)
        {
            case FlipMode.Never:  return false;
            case FlipMode.Always: return true;
            default:              return SystemInfo.graphicsUVStartsAtTop;
        }
    }

    /// Full-screen RawImage as the canvas's first child, so it sits behind the
    /// panel. It also eats clicks, which is what makes the panel modal.
    private void EnsureBackdrop()
    {
        if (_backdrop != null) return;

        var existing = transform.Find("Backdrop");
        if (existing != null) _backdrop = existing.GetComponent<RawImage>();

        if (_backdrop == null)
        {
            var go = new GameObject("Backdrop", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            _backdrop = go.AddComponent<RawImage>();
        }

        _backdrop.transform.SetAsFirstSibling();
        _backdrop.raycastTarget = true;

        var rt = _backdrop.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}

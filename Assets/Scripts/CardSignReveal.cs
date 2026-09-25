using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The moment a word on a card turns into SignWriting. A band of light sweeps
// across the card, and as it passes the word swaps for its sign - which pops,
// glows and throws off a few sparkles, so the player sees exactly which word
// just changed instead of noticing later that something is different.
//
// Added to a card on demand by CardDisplay.RevealSign. Everything it draws sits
// inside MaskedBody, so the card's rounded mask clips it like the rest of the art.
[DisallowMultipleComponent]
public class CardSignReveal : MonoBehaviour
{
    private const float SweepDuration = 0.85f;
    private const float SwapAt = 0.45f;        // how far through the sweep the word changes
    private const float BurstDuration = 0.75f;
    private const float PopScale = 1.35f;
    private static readonly Color LightColor = new Color(1f, 0.95f, 0.78f, 1f);
    // Gold rather than white: most card art is pale around the words, and a
    // white glow or sparkle simply disappears into it.
    private static readonly Color GlowColor = new Color(1f, 0.78f, 0.28f, 1f);
    private static readonly Color SparkColor = new Color(1f, 0.86f, 0.45f, 1f);

    private CardDisplay _card;
    private string _word;
    private bool _swapPending;
    private Coroutine _run;

    private RectTransform _body;
    private Image _sweep;
    private readonly List<Image> _fx = new List<Image>();
    private readonly List<CardDisplay.WordSpot> _spots = new List<CardDisplay.WordSpot>();
    private readonly List<RectTransform> _popped = new List<RectTransform>();
    private readonly List<Vector3> _poppedScale = new List<Vector3>();

    private static Sprite _bandSprite;
    private static Sprite _glowSprite;
    private static Sprite _starSprite;

    public void Play(CardDisplay card, string word, float delay)
    {
        Swap();   // an earlier reveal that never reached its swap
        _card = card;
        _word = word;
        _swapPending = true;

        if (!isActiveAndEnabled) { Swap(); return; }
        if (_run != null) StopCoroutine(_run);
        _run = StartCoroutine(Run(delay));
    }

    private void OnDisable()
    {
        // Leaving mid-reveal (played, discarded): the word still has to change.
        Swap();
        _run = null;
        RestorePopped();
        if (_sweep != null) _sweep.gameObject.SetActive(false);
        for (int i = 0; i < _fx.Count; i++) if (_fx[i] != null) _fx[i].gameObject.SetActive(false);
    }

    private void Swap()
    {
        if (!_swapPending) return;
        _swapPending = false;
        if (_card != null) _card.RefreshSignText();
    }

    private IEnumerator Run(float delay)
    {
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        if (!EnsureBody()) { Swap(); _run = null; yield break; }

        Image sweep = Sweep();
        RectTransform srt = sweep.rectTransform;
        float travel = _body.rect.width * 0.5f + srt.rect.width;
        bool burst = false;

        for (float t = 0f; t < SweepDuration; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.Clamp01(t / SweepDuration);
            float e = p * p * (3f - 2f * p);
            srt.anchoredPosition = new Vector2(Mathf.Lerp(-travel, travel, e), 0f);

            Color c = LightColor;
            c.a = 0.85f * Mathf.Sin(p * Mathf.PI);
            sweep.color = c;

            if (!burst && p >= SwapAt)
            {
                burst = true;
                Swap();
                StartCoroutine(Burst());
            }
            yield return null;
        }

        sweep.gameObject.SetActive(false);
        if (!burst) { Swap(); StartCoroutine(Burst()); }
        _run = null;
    }

    // The new sign pops, glows and sparkles wherever it now appears on the card.
    private IEnumerator Burst()
    {
        yield return null;   // let the rows lay out the new sign before measuring it

        _spots.Clear();
        _card.CollectWordSpots(_spots);
        string key = SignWritingLibrary.Normalize(_word);

        var rects = new List<Rect>();
        RestorePopped();
        for (int i = 0; i < _spots.Count; i++)
        {
            var s = _spots[i];
            if (!s.isSign || SignWritingLibrary.Normalize(s.key) != key) continue;

            Vector2 a = _body.InverseTransformPoint(s.bottomLeft);
            Vector2 b = _body.InverseTransformPoint(s.topRight);
            rects.Add(Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y)));

            if (s.element != null && !_popped.Contains(s.element))
            {
                _popped.Add(s.element);
                _poppedScale.Add(s.element.localScale);
            }
        }
        if (rects.Count == 0) yield break;

        // One glow and three sparkles per place the word shows up.
        const int starsPer = 3;
        var glows = new List<Image>();
        var stars = new List<Image>();
        var starDelay = new List<float>();
        var starSpin = new List<float>();
        int used = 0;

        foreach (var r in rects)
        {
            Image g = Fx(used++, GlowSprite());
            float size = Mathf.Max(r.width, r.height) * 3f + 30f;
            g.rectTransform.sizeDelta = new Vector2(size, size);
            g.rectTransform.anchoredPosition = r.center;
            glows.Add(g);

            for (int k = 0; k < starsPer; k++)
            {
                Image s = Fx(used++, StarSprite());
                float sz = Random.Range(20f, 32f);
                s.rectTransform.sizeDelta = new Vector2(sz, sz);
                s.rectTransform.anchoredPosition = r.center + new Vector2(
                    Random.Range(-0.65f, 0.65f) * (r.width + 16f),
                    Random.Range(-0.65f, 0.65f) * (r.height + 16f));
                stars.Add(s);
                starDelay.Add(k * 0.08f + Random.Range(0f, 0.05f));
                starSpin.Add(Random.Range(60f, 140f) * (Random.value < 0.5f ? -1f : 1f));
            }
        }

        for (float t = 0f; t < BurstDuration; t += Time.unscaledDeltaTime)
        {
            float p = Mathf.Clamp01(t / BurstDuration);

            // Glow: blooms fast, fades slow.
            for (int i = 0; i < glows.Count; i++)
            {
                Color c = GlowColor;
                c.a = 0.95f * (1f - p) * (1f - p);
                glows[i].color = c;
                glows[i].rectTransform.localScale = Vector3.one * Mathf.Lerp(0.55f, 1.25f, 1f - (1f - p) * (1f - p));
            }

            // Sparkles: each twinkles in and out on its own beat.
            for (int i = 0; i < stars.Count; i++)
            {
                float sp = Mathf.Clamp01((t - starDelay[i]) / 0.45f);
                float tw = Mathf.Sin(sp * Mathf.PI);
                stars[i].color = new Color(SparkColor.r, SparkColor.g, SparkColor.b, tw);
                stars[i].rectTransform.localScale = Vector3.one * tw;
                stars[i].rectTransform.localRotation = Quaternion.Euler(0f, 0f, starSpin[i] * sp);
            }

            // The sign itself pops, overshooting a touch before settling.
            float pop = p < 0.45f ? Mathf.Sin(p / 0.45f * Mathf.PI) : 0f;
            for (int i = 0; i < _popped.Count; i++)
                if (_popped[i] != null)
                    _popped[i].localScale = _poppedScale[i] * Mathf.Lerp(1f, PopScale, pop);

            yield return null;
        }

        for (int i = 0; i < used; i++) _fx[i].gameObject.SetActive(false);
        RestorePopped();
    }

    private void RestorePopped()
    {
        for (int i = 0; i < _popped.Count; i++)
            if (_popped[i] != null) _popped[i].localScale = _poppedScale[i];
        _popped.Clear();
        _poppedScale.Clear();
    }

    // ---- building the pieces -----------------------------------------------

    private bool EnsureBody()
    {
        if (_body == null) _body = transform.Find("MaskedBody") as RectTransform;
        if (_body == null) _body = transform as RectTransform;
        return _body != null && _card != null;
    }

    private Image Sweep()
    {
        if (_sweep == null)
        {
            _sweep = NewImage("SignRevealSweep", BandSprite());
            var rt = _sweep.rectTransform;
            float h = Mathf.Sqrt(_body.rect.width * _body.rect.width + _body.rect.height * _body.rect.height);
            rt.sizeDelta = new Vector2(_body.rect.width * 0.42f, h);
            rt.localRotation = Quaternion.Euler(0f, 0f, -18f);
        }
        _sweep.gameObject.SetActive(true);
        _sweep.transform.SetAsLastSibling();   // above the words, and above the dim overlay
        return _sweep;
    }

    private Image Fx(int index, Sprite sprite)
    {
        while (_fx.Count <= index) _fx.Add(NewImage("SignRevealFx", null));
        Image img = _fx[index];
        img.sprite = sprite;
        img.color = new Color(1f, 1f, 1f, 0f);
        img.rectTransform.localScale = Vector3.one;
        img.rectTransform.localRotation = Quaternion.identity;
        img.gameObject.SetActive(true);
        img.transform.SetAsLastSibling();
        return img;
    }

    private Image NewImage(string objectName, Sprite sprite)
    {
        var go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(_body, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;   // must never swallow a click on the card
        img.color = new Color(1f, 1f, 1f, 0f);
        return img;
    }

    // ---- procedural sprites ------------------------------------------------

    // Soft vertical band of light with a brighter core.
    private static Sprite BandSprite()
    {
        if (_bandSprite == null)
            _bandSprite = MakeSprite("SignRevealBand", 64, 8, (u, v) =>
            {
                float x = (u - 0.5f);
                return 0.7f * Mathf.Exp(-x * x / 0.03f) + 0.3f * Mathf.Exp(-x * x / 0.002f);
            });
        return _bandSprite;
    }

    private static Sprite GlowSprite()
    {
        if (_glowSprite == null)
            _glowSprite = MakeSprite("SignRevealGlow", 64, 64, (u, v) =>
            {
                float dx = u * 2f - 1f, dy = v * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                return Mathf.Pow(Mathf.Clamp01(1f - r), 2f);
            });
        return _glowSprite;
    }

    // Four-pointed twinkle.
    private static Sprite StarSprite()
    {
        if (_starSprite == null)
            _starSprite = MakeSprite("SignRevealStar", 48, 48, (u, v) =>
            {
                float dx = Mathf.Abs(u * 2f - 1f), dy = Mathf.Abs(v * 2f - 1f);
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float rays = Mathf.Clamp01(1f - dx * dy * 28f) * Mathf.Clamp01(1f - r);
                float core = Mathf.Exp(-r * r * 30f);
                return Mathf.Clamp01(Mathf.Pow(rays, 3f) + core);
            });
        return _starSprite;
    }

    private static Sprite MakeSprite(string spriteName, int w, int h, System.Func<float, float, float> alphaAt)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            name = spriteName,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float a = Mathf.Clamp01(alphaAt((x + 0.5f) / w, (y + 0.5f) / h));
                px[y * w + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        tex.SetPixels32(px);
        tex.Apply();

        var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = spriteName;
        return sprite;
    }
}

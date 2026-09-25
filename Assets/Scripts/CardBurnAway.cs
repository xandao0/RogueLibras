using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// A one-shot "the card burns up where it sat" effect. It runs on a throwaway
// visual clone of a played card, so the real card can go straight to the discard
// pile untouched. Spawn with CardBurnAway.Spawn(sourceCard).
public class CardBurnAway : MonoBehaviour
{
    [SerializeField] private float duration = 0.82f;
    [SerializeField] private Material fireMaterial;   // RogueLibras/UI Fire

    private static Transform _overlay;

    // One dissolve material per graphic on the clone, so the whole card is eaten
    // by the same advancing burn front instead of just fading out.
    private readonly List<Material> _dissolves = new List<Material>();
    // Text cannot take the dissolve shader, so it is faded out in step with it.
    private readonly List<Graphic> _texts = new List<Graphic>();

    // Builds the clone, strips anything interactive, and starts the burn.
    public static void Spawn(CardDisplay source, Material fire)
    {
        if (source == null) return;

        var src = source.gameObject;
        var clone = Instantiate(src);
        clone.name = "BurningCard";

        // Kill everything that could touch game state or spam the log.
        StripBehaviours(clone);

        var overlay = GetOverlay(source);
        var srcRt = (RectTransform)src.transform;
        var rt = (RectTransform)clone.transform;
        rt.SetParent(overlay, false);
        rt.position     = srcRt.position;
        rt.rotation     = srcRt.rotation;
        rt.localScale   = srcRt.lossyScale;   // overlay is unscaled, so lossy == local
        rt.SetAsLastSibling();

        var burn = clone.AddComponent<CardBurnAway>();
        burn.fireMaterial = fire;
        burn.StartCoroutine(burn.Run());
    }

    private static void StripBehaviours(GameObject clone)
    {
        foreach (var mb in clone.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb is CardBurnAway) continue;
            Destroy(mb);
        }
        foreach (var vp in clone.GetComponentsInChildren<UnityEngine.Video.VideoPlayer>(true))
            Destroy(vp);
        foreach (var g in clone.GetComponentsInChildren<Graphic>(true))
            g.raycastTarget = false;

        var cg = clone.GetComponent<CanvasGroup>();
        if (cg == null) cg = clone.AddComponent<CanvasGroup>();
        cg.interactable = false;
        cg.blocksRaycasts = false;
    }

    private static Transform GetOverlay(CardDisplay source)
    {
        if (_overlay != null) return _overlay;

        var canvas = source.GetComponentInParent<Canvas>();
        var root = canvas != null ? canvas.rootCanvas.transform : source.transform.root;

        var go = new GameObject("BurningCardOverlay", typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(root, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        rt.SetAsLastSibling();
        _overlay = rt;
        return _overlay;
    }

    // Swap every graphic on the clone onto the dissolve shader. Each keeps its
    // own sprite; they share a progress value, so the burn crosses the card as
    // one front rather than each layer dying on its own schedule.
    private void BuildDissolves()
    {
        Shader shader = Shader.Find("RogueLibras/UI Dissolve");
        if (shader == null) return;

        foreach (var g in GetComponentsInChildren<Graphic>(true))
        {
            if (g.transform.name == "Fire") continue;          // the flame is not burning
            // TMP needs its own shader - handing text this one renders garbage.
            if (g is TMPro.TMP_Text) { _texts.Add(g); continue; }

            var m = new Material(shader) { name = "CardBurn" };
            m.SetFloat("_Dissolve", 0f);
            g.material = m;
            _dissolves.Add(m);
        }
    }

    private void SetDissolve(float v)
    {
        for (int i = 0; i < _dissolves.Count; i++) _dissolves[i].SetFloat("_Dissolve", v);

        // Text goes a little ahead of the burn front so the card is not left
        // with words floating over a hole.
        float textAlpha = Mathf.Clamp01(1f - v * 1.6f);
        for (int i = 0; i < _texts.Count; i++)
        {
            if (_texts[i] == null) continue;
            Color c = _texts[i].color;
            c.a = textAlpha;
            _texts[i].color = c;
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < _dissolves.Count; i++)
            if (_dissolves[i] != null) Destroy(_dissolves[i]);
    }

    private IEnumerator Run()
    {
        var cg = GetComponent<CanvasGroup>();
        BuildDissolves();

        // Fire quad over the whole card.
        var fireGO = new GameObject("Fire", typeof(RectTransform));
        var frt = (RectTransform)fireGO.transform;
        frt.SetParent(transform, false);
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
        frt.offsetMin = new Vector2(-12f, -12f);
        frt.offsetMax = new Vector2(12f, 30f);   // flames lick past the top edge
        var fireImg = fireGO.AddComponent<Image>();
        fireImg.raycastTarget = false;
        if (fireMaterial != null) fireImg.material = fireMaterial;
        fireImg.color = new Color(1f, 1f, 1f, 0f);

        Vector3 basePos = transform.position;
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / duration);

            // fire: quick flare, slow fade
            float fire = p < 0.25f ? Mathf.SmoothStep(0f, 1f, p / 0.25f)
                                   : Mathf.SmoothStep(1f, 0f, (p - 0.25f) / 0.75f);
            fireImg.color = new Color(1f, 1f, 1f, fire);

            // card: hold briefly, then burn away along a noisy front. It drifts
            // up a little as it goes, but the dissolve does the disappearing -
            // the alpha fade is only there for whatever the shader missed.
            float eat = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((p - 0.12f) / 0.88f));
            SetDissolve(eat);
            transform.position = basePos + Vector3.up * (eat * 22f);
            if (cg != null) cg.alpha = Mathf.Clamp01(1f - (eat - 0.85f) / 0.15f);

            yield return null;
        }

        Destroy(gameObject);
    }
}

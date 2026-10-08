using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// A speaker button that mutes and unmutes the whole game (GameSettings.Muted).
//
// Every scene gets one without anything authored: when a scene loads, one is
// installed next to the battle's Sair button - same size, colour and font,
// stacked underneath it - or, in a scene without one (the menu), in the
// top-right corner. A scene that already has a MuteButton placed by hand keeps
// that one and gets nothing extra.
[RequireComponent(typeof(RectTransform))]
public class MuteButton : MonoBehaviour
{
    private static readonly Color PlateColor = new Color(0.2f, 0.16f, 0.14f, 0.85f);
    private static readonly Color InkColor = new Color(0.96f, 0.93f, 0.85f, 1f);

    private const float Gap = 12f;   // the gap between Opções and Sair in battle

    private Image _icon;
    private TMP_Text _label;

    private static Sprite _onSprite;
    private static Sprite _offSprite;

    // ---- installing itself -------------------------------------------------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Install();   // the first scene has already loaded by now
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) Install();
    }

    private static void Install()
    {
        if (FindObjectOfType<MuteButton>(true) != null) return;

        Button exit = FindButtonCalling("LoadMainMenu");
        if (exit != null) InstallBelow((RectTransform)exit.transform);
        else InstallInCorner();
    }

    // Battle: a twin of the Sair button, one slot further down the stack.
    private static void InstallBelow(RectTransform anchor)
    {
        RectTransform rt = Create(anchor.parent);
        rt.SetSiblingIndex(anchor.GetSiblingIndex() + 1);
        rt.anchorMin = anchor.anchorMin;
        rt.anchorMax = anchor.anchorMax;
        rt.pivot = anchor.pivot;
        rt.sizeDelta = anchor.sizeDelta;
        rt.anchoredPosition = anchor.anchoredPosition - new Vector2(0f, anchor.rect.height + Gap);

        var mb = rt.GetComponent<MuteButton>();
        var plate = anchor.GetComponent<Image>();
        Color color = plate != null ? plate.color : PlateColor;
        TMP_Text font = anchor.GetComponentInChildren<TMP_Text>(true);
        mb.Build(color, font);
        LanguageButton.InstallNextTo(rt, true, color, font);
    }

    // Menu: top-right corner of the main canvas, drawn just after the menu
    // itself so anything opened later (the settings panel) still covers it.
    private static void InstallInCorner()
    {
        Canvas canvas = null;
        Button play = FindButtonCalling("PlayGame");
        if (play != null) canvas = play.GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null) return;
        canvas = canvas.rootCanvas;

        RectTransform rt = Create(canvas.transform);
        if (play != null)
        {
            Transform branch = play.transform;
            while (branch.parent != null && branch.parent != canvas.transform) branch = branch.parent;
            if (branch.parent == canvas.transform) rt.SetSiblingIndex(branch.GetSiblingIndex() + 1);
        }

        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(140f, 70f);
        rt.anchoredPosition = new Vector2(-30f, -25f);

        TMP_Text fontSource = play != null ? play.GetComponentInChildren<TMP_Text>(true) : null;
        rt.GetComponent<MuteButton>().Build(PlateColor, fontSource);
        LanguageButton.InstallNextTo(rt, false, PlateColor, fontSource);
    }

    private static RectTransform Create(Transform parent)
    {
        var go = new GameObject("MuteButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(MuteButton));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    private static Button FindButtonCalling(string method)
    {
        foreach (var b in FindObjectsOfType<Button>(true))
            for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++)
                if (b.onClick.GetPersistentMethodName(i) == method) return b;
        return null;
    }

    // ---- the button --------------------------------------------------------

    private void Build(Color plateColor, TMP_Text fontSource)
    {
        var plate = GetComponent<Image>();
        plate.color = plateColor;

        var button = GetComponent<Button>();
        button.targetGraphic = plate;
        button.onClick.AddListener(GameSettings.ToggleMute);

        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(transform, false);
        var irt = (RectTransform)iconGo.transform;
        irt.anchorMin = irt.anchorMax = new Vector2(0f, 0.5f);
        irt.pivot = new Vector2(0f, 0.5f);
        irt.sizeDelta = new Vector2(40f, 40f);
        irt.anchoredPosition = new Vector2(12f, 0f);
        _icon = iconGo.GetComponent<Image>();
        _icon.raycastTarget = false;
        _icon.color = InkColor;

        var labelGo = new GameObject("Text", typeof(RectTransform));
        labelGo.transform.SetParent(transform, false);
        var lrt = (RectTransform)labelGo.transform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = new Vector2(54f, 0f);
        lrt.offsetMax = new Vector2(-8f, 0f);
        _label = labelGo.AddComponent<TextMeshProUGUI>();
        if (fontSource != null)
        {
            _label.font = fontSource.font;
            _label.color = fontSource.color;
        }
        else _label.color = InkColor;
        _label.fontSize = 26f;
        _label.enableAutoSizing = true;
        _label.fontSizeMin = 14f;
        _label.fontSizeMax = 26f;
        _label.alignment = TextAlignmentOptions.Center;
        _label.raycastTarget = false;

        Refresh();
    }

    private void OnEnable()
    {
        GameSettings.Changed += Refresh;
        Loc.Changed += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        GameSettings.Changed -= Refresh;
        Loc.Changed -= Refresh;
    }

    private void Refresh()
    {
        if (_icon == null) return;
        bool muted = GameSettings.Muted;
        _icon.sprite = muted ? OffSprite() : OnSprite();
        // Bilingual like the rest of the menus: the sign above the word.
        SignText.UsePlainSigns(_label);
        if (_label != null) _label.text = Loc.Signed(muted ? "audio.sound_off" : "audio.sound_on", SignText.Layout.Above, 1.2f);
    }

    // ---- procedural speaker icons -----------------------------------------

    private static Sprite OnSprite()
    {
        if (_onSprite == null) _onSprite = MakeIcon("SpeakerOn", false);
        return _onSprite;
    }

    private static Sprite OffSprite()
    {
        if (_offSprite == null) _offSprite = MakeIcon("SpeakerOff", true);
        return _offSprite;
    }

    private static Sprite MakeIcon(string spriteName, bool muted)
    {
        const int size = 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = spriteName,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        float px = 1f / size;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = (x + 0.5f) / size, v = (y + 0.5f) / size;

                // Signed distance (negative inside) to the speaker, then to
                // either the sound waves or the cross.
                float d = Speaker(u, v);
                d = Mathf.Min(d, muted ? Cross(u, v) : Waves(u, v));

                float a = Mathf.Clamp01(0.5f - d / px);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }

        tex.SetPixels32(pixels);
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = spriteName;
        return sprite;
    }

    private static float Speaker(float u, float v)
    {
        float dy = Mathf.Abs(v - 0.5f);

        // the magnet: a small box
        float box = Mathf.Max(Mathf.Max(0.10f - u, u - 0.30f), dy - 0.12f);

        // the cone: widens from the box out to the right
        float halfHeight = 0.12f + (u - 0.30f) / 0.22f * 0.22f;
        float cone = Mathf.Max(Mathf.Max(0.29f - u, u - 0.52f), dy - halfHeight);

        return Mathf.Min(box, cone);
    }

    private static float Waves(float u, float v)
    {
        float dx = u - 0.48f, dy = v - 0.5f;
        float r = Mathf.Sqrt(dx * dx + dy * dy);
        float angle = Mathf.Abs(Mathf.Atan2(dy, dx)) * Mathf.Rad2Deg;
        if (dx <= 0f || angle > 50f) return 1f;

        float inner = Mathf.Abs(r - 0.18f) - 0.035f;
        float outer = Mathf.Abs(r - 0.32f) - 0.035f;
        return Mathf.Min(inner, outer);
    }

    private static float Cross(float u, float v)
    {
        float cx = u - 0.76f, cy = v - 0.5f;
        if (Mathf.Abs(cx) > 0.15f || Mathf.Abs(cy) > 0.15f) return 1f;
        const float invSqrt2 = 0.70710678f;
        float a = Mathf.Abs(cx - cy) * invSqrt2;
        float b = Mathf.Abs(cx + cy) * invSqrt2;
        return Mathf.Min(a, b) - 0.04f;
    }
}

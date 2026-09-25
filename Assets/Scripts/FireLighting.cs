using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Fakes firelight over an otherwise unlit 2D scene. Everything registered here
// breathes between a dim and a bright warm tone, driven by three layers of
// Perlin noise at unrelated rates so the flicker never settles into an audible
// loop, plus the occasional flare for when the fire catches.
//
// Whatever colour a target already has is treated as its unlit base, so this
// composes with art that is not pure white.
//
// The enemy art is instantiated at runtime, so anything parented under
// runtimeRoot is picked up automatically as it appears.
[DisallowMultipleComponent]
public class FireLighting : MonoBehaviour
{
    [Header("Targets")]
    [Tooltip("Sprites lit by the fire.")]
    [SerializeField] private SpriteRenderer[] sprites;

    [Tooltip("Canvas graphics lit by the fire, for UI art that shares the scene's light.")]
    [SerializeField] private Graphic[] graphics;

    [Tooltip("Sprites spawned under this transform are picked up automatically. " +
             "Point it at the enemy spawn so the monster is lit like everything else.")]
    [SerializeField] private Transform runtimeRoot;

    [Tooltip("How often to look for newly spawned art under Runtime Root.")]
    [SerializeField] private float runtimeScanInterval = 0.5f;

    [Header("Flicker")]
    [Tooltip("Darkest the scene gets between flickers.")]
    [SerializeField] private float minBrightness = 0.82f;

    [Tooltip("Brightest the scene gets between flickers.")]
    [SerializeField] private float maxBrightness = 1.12f;

    [Tooltip("Overall flicker rate. 1 is a calm hearth, 3 is a torch in wind.")]
    [SerializeField] private float speed = 1f;

    [Tooltip("Colour the light pushes toward at its brightest.")]
    [SerializeField] private Color warmTint = new Color(1f, 0.78f, 0.55f, 1f);

    [Range(0f, 1f)]
    [Tooltip("How far toward Warm Tint a fully bright flicker pushes.")]
    [SerializeField] private float tintStrength = 0.35f;

    [Header("Flare")]
    [Tooltip("Random seconds between flares. Set both to 0 to disable flares.")]
    [SerializeField] private Vector2 flareInterval = new Vector2(4f, 11f);
    [SerializeField] private float flareBoost = 0.16f;
    [SerializeField] private float flareDuration = 0.6f;

    private readonly List<SpriteRenderer> _spriteTargets = new List<SpriteRenderer>();
    private readonly List<Color> _spriteBase = new List<Color>();
    private readonly List<Graphic> _graphicTargets = new List<Graphic>();
    private readonly List<Color> _graphicBase = new List<Color>();

    private float _seed;
    private float _time;
    private float _nextScan;
    private float _nextFlare;
    private float _flareEndsAt = -1f;

    private void Awake()
    {
        _seed = Random.Range(0f, 500f);

        if (sprites != null)
            foreach (var s in sprites) AddSprite(s);

        if (graphics != null)
        {
            foreach (var g in graphics)
            {
                if (g == null || _graphicTargets.Contains(g)) continue;
                _graphicTargets.Add(g);
                _graphicBase.Add(g.color);
            }
        }

        ScheduleFlare();
    }

    private void OnDisable()
    {
        // Leave the scene at its unlit base rather than frozen mid-flicker.
        for (int i = 0; i < _spriteTargets.Count; i++)
            if (_spriteTargets[i] != null) _spriteTargets[i].color = _spriteBase[i];

        for (int i = 0; i < _graphicTargets.Count; i++)
            if (_graphicTargets[i] != null) _graphicTargets[i].color = _graphicBase[i];
    }

    private void LateUpdate()
    {
        float dt = Time.unscaledDeltaTime;
        _time += dt * Mathf.Max(0f, speed);

        if (runtimeRoot != null && Time.unscaledTime >= _nextScan)
        {
            _nextScan = Time.unscaledTime + Mathf.Max(0.1f, runtimeScanInterval);
            ScanRuntimeRoot();
        }

        // Three octaves at unrelated rates: a slow swell, a body flicker, and a
        // fast crackle. Perlin sits around 0.5, so recentre and widen the result
        // to actually reach both ends of the range.
        float n = Mathf.PerlinNoise(_time * 0.55f, _seed) * 0.58f
                + Mathf.PerlinNoise(_time * 1.90f, _seed + 17.3f) * 0.29f
                + Mathf.PerlinNoise(_time * 5.30f, _seed + 41.7f) * 0.13f;
        n = Mathf.Clamp01((n - 0.5f) * 1.7f + 0.5f);

        float flare = 0f;
        if (Time.unscaledTime < _flareEndsAt)
        {
            float p = 1f - (_flareEndsAt - Time.unscaledTime) / Mathf.Max(0.01f, flareDuration);
            flare = Mathf.Sin(Mathf.Clamp01(p) * Mathf.PI) * flareBoost;
        }
        else if (Time.unscaledTime >= _nextFlare && flareBoost > 0f)
        {
            _flareEndsAt = Time.unscaledTime + flareDuration;
            ScheduleFlare();
        }

        float brightness = Mathf.Lerp(minBrightness, maxBrightness, n) + flare;
        Color tint = Color.Lerp(Color.white, warmTint, tintStrength * n);

        for (int i = 0; i < _spriteTargets.Count; i++)
        {
            var sr = _spriteTargets[i];
            if (sr == null) continue;
            sr.color = Light(_spriteBase[i], tint, brightness);
        }

        for (int i = 0; i < _graphicTargets.Count; i++)
        {
            var g = _graphicTargets[i];
            if (g == null) continue;
            g.color = Light(_graphicBase[i], tint, brightness);
        }
    }

    private static Color Light(Color baseColor, Color tint, float brightness)
    {
        return new Color(baseColor.r * tint.r * brightness,
                         baseColor.g * tint.g * brightness,
                         baseColor.b * tint.b * brightness,
                         baseColor.a);
    }

    private void ScanRuntimeRoot()
    {
        // Each defeated enemy leaves a destroyed renderer behind, so drop those
        // before adding the new one -- otherwise the list grows all run.
        for (int i = _spriteTargets.Count - 1; i >= 0; i--)
        {
            if (_spriteTargets[i] != null) continue;
            _spriteTargets.RemoveAt(i);
            _spriteBase.RemoveAt(i);
        }

        var found = runtimeRoot.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < found.Length; i++) AddSprite(found[i]);
    }

    private void AddSprite(SpriteRenderer sr)
    {
        if (sr == null || _spriteTargets.Contains(sr)) return;
        _spriteTargets.Add(sr);
        _spriteBase.Add(sr.color);
    }

    private void ScheduleFlare()
    {
        float lo = Mathf.Min(flareInterval.x, flareInterval.y);
        float hi = Mathf.Max(flareInterval.x, flareInterval.y);
        _nextFlare = Time.unscaledTime + Random.Range(Mathf.Max(0.5f, lo), Mathf.Max(1f, hi));
    }
}

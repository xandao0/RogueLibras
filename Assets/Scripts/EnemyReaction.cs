using System;
using System.Collections;
using UnityEngine;

// Gives the enemy something to *do*, so a turn reads as an event instead of
// numbers quietly changing.
//
// At rest the enemy breathes. On its turn it performs its intent in four beats:
//
//   Wind-up    - the "waiting to act" pose (EnemyDataSO wind-up frames), with a
//                tremble and a charge that build toward the move
//   Telegraph  - a hot flash and an aura ring bursting outward: the tell that
//                the move is coming right now
//   Move       - the attack frames with a lunge (damage lands here), or the
//                guard frames with a swell (block is gained here)
//   Settle     - back to the resting pose
//
// Between turns there are three short reactions:
//
//   Attack  - lunges toward the player and flashes hot white
//   Defend  - pulses cold blue and swells slightly
//   Hurt    - snaps back from the blow and flashes red
//
// Everything drives the same sprite through one set of pose values applied in
// LateUpdate, so the beats cannot fight each other. An enemy with no frames
// authored still performs every beat on its resting pose.
[DisallowMultipleComponent]
public class EnemyReaction : MonoBehaviour
{
    [Tooltip("The enemy art. Found on the Sprite child if left empty. Its material " +
             "should use RogueLibras/Sprite Flash, or the flashes won't show.")]
    [SerializeField] private SpriteRenderer art;

    [Header("Idle")]
    [Tooltip("How much the resting pose breathes, as a fraction of its height.")]
    [SerializeField] private float breatheAmount = 0.012f;
    [Tooltip("Breaths per second.")]
    [SerializeField] private float breatheRate = 0.35f;

    [Header("Wind-up (getting ready to act)")]
    [Tooltip("Seconds on each wind-up frame before the last one.")]
    [SerializeField] private float windupFrameTime = 0.3f;
    [Tooltip("Seconds the last wind-up frame is held - coiled, waiting to act.")]
    [SerializeField] private float windupHold = 0.45f;
    [Tooltip("How hard the enemy trembles by the end of the wind-up, in local units.")]
    [SerializeField] private float windupTremble = 1.6f;

    [Header("Telegraph (the tell right before the move)")]
    [SerializeField] private float telegraphDuration = 0.2f;
    [SerializeField] private float telegraphPop = 1.05f;
    [SerializeField] private float auraRingDuration = 0.45f;
    [SerializeField] private Color attackAura = new Color(1f, 0.30f, 0.12f, 1f);
    [SerializeField] private Color defendAura = new Color(0.35f, 0.72f, 1f, 1f);

    [Header("Attack")]
    [Tooltip("How far the enemy lunges, in local units. Negative Y reads as coming at the player.")]
    [SerializeField] private Vector2 attackLunge = new Vector2(0f, -26f);
    [SerializeField] private float attackGrow = 1.06f;
    [Tooltip("Seconds on each attack frame before the last one.")]
    [SerializeField] private float attackFrameTime = 0.16f;
    [Tooltip("Seconds the last attack frame is held.")]
    [SerializeField] private float attackHold = 0.5f;
    [Tooltip("Length of the lunge when the enemy has no attack frames.")]
    [SerializeField] private float attackDuration = 0.42f;
    [SerializeField] private Color attackFlash = new Color(1f, 0.94f, 0.80f, 1f);

    [Header("Defend")]
    [Tooltip("Seconds on each guard frame before the last one.")]
    [SerializeField] private float defendFrameTime = 0.16f;
    [Tooltip("Seconds the last guard frame is held.")]
    [SerializeField] private float defendHold = 0.6f;
    [Tooltip("Length of the swell when the enemy has no guard frames.")]
    [SerializeField] private float defendDuration = 0.55f;
    [SerializeField] private float defendSwell = 1.07f;
    [SerializeField] private Color defendFlash = new Color(0.62f, 0.84f, 1f, 1f);

    [Header("Hurt")]
    [SerializeField] private Vector2 hurtRecoil = new Vector2(0f, 14f);
    [SerializeField] private float hurtDuration = 0.30f;
    [SerializeField] private Color hurtFlash = new Color(1f, 0.35f, 0.30f, 1f);

    private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
    private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");

    private Enemy _enemy;
    private Vector3 _restPos;
    private Vector3 _restScale;
    private bool _hasRest;

    // The pose, written by whichever beat is running and applied in LateUpdate.
    private Vector2 _offset;
    private Vector2 _scale = Vector2.one;
    private Vector2 _tremble;
    private float _punch;          // brief squash when the frame changes
    private float _breathe = 1f;   // 1 at rest, 0 while acting
    private Color _flashColor = Color.white;
    private float _flash;

    private bool _performing;
    private Coroutine _beat;

    private MaterialPropertyBlock _mpb;
    private Color _appliedFlashColor;
    private float _appliedFlash = -1f;

    // Aura: a glow behind the enemy and a ring that bursts on the telegraph.
    // It lives at the scene root on purpose - FireLighting adopts every sprite
    // under the enemy spawn and rewrites its colour, which would pin the aura's
    // alpha at whatever it was the moment it was found.
    private Transform _auraRoot;
    private SpriteRenderer _glow;
    private SpriteRenderer _ring;
    private Color _auraColor = Color.white;
    private float _glowAlpha;
    private float _glowScale = 1f;
    private float _ringT = -1f;
    private static Sprite _glowSprite;
    private static Sprite _ringSprite;

    /// True while the enemy is taking its turn.
    public bool IsPerforming { get { return _performing; } }

    private EnemyDataSO Data { get { return _enemy != null ? _enemy.eData : null; } }

    // The pose every other frame is aligned to.
    private Sprite IdleSprite
    {
        get
        {
            var d = Data;
            if (d != null && d.artwork != null) return d.artwork;
            return art != null ? art.sprite : null;
        }
    }

    private void Awake()
    {
        _enemy = GetComponent<Enemy>();

        if (art == null)
        {
            var t = transform.Find("Sprite");
            if (t != null) art = t.GetComponent<SpriteRenderer>();
            if (art == null) art = GetComponentInChildren<SpriteRenderer>(true);
        }
        if (art != null)
        {
            _restPos = art.transform.localPosition;
            _restScale = art.transform.localScale;
            _hasRest = true;
        }
    }

    private void OnDestroy()
    {
        if (_auraRoot != null) Destroy(_auraRoot.gameObject);
    }

    // ---- reactions between turns ------------------------------------------

    public void PlayAttack()
    {
        Play(attackLunge, attackDuration, attackFlash, 0.4f, attackGrow);
    }

    public void PlayDefend()
    {
        // During its own turn the guard is already part of the performance.
        if (_performing) return;
        Play(Vector2.zero, defendDuration, defendFlash, 0.4f, defendSwell);
    }

    public void PlayHurt()
    {
        if (_performing) return;
        Play(hurtRecoil, hurtDuration, hurtFlash, 0.55f, 1f);
    }

    private void Play(Vector2 offset, float duration, Color flash, float strength, float swell)
    {
        if (art == null || !isActiveAndEnabled) return;
        StopBeat();
        _beat = StartCoroutine(Beat(offset, duration, flash, strength, swell));
    }

    private void StopBeat()
    {
        if (_beat != null) StopCoroutine(_beat);
        _beat = null;
        _offset = Vector2.zero;
        _scale = Vector2.one;
        _flash = 0f;
    }

    private IEnumerator Beat(Vector2 offset, float duration, Color flash, float strength, float swell)
    {
        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            float shape = Snap(Mathf.Clamp01(t / duration), 0.35f);
            _offset = offset * shape;
            _scale = Vector2.one * Mathf.Lerp(1f, swell, shape);
            _flashColor = flash;
            _flash = strength * shape;
            yield return null;
        }

        _offset = Vector2.zero;
        _scale = Vector2.one;
        _flash = 0f;
        _beat = null;
    }

    // Out fast, back slow: rises to 1 by `peak`, then eases back to 0.
    private static float Snap(float p, float peak)
    {
        return p < peak
            ? Mathf.SmoothStep(0f, 1f, p / peak)
            : Mathf.SmoothStep(1f, 0f, (p - peak) / (1f - peak));
    }

    // ---- the enemy's turn ---------------------------------------------------

    /// Plays the whole turn for one intent. onImpact applies its effect (the
    /// damage, the block) at the moment the move lands, and is always called
    /// exactly once - even if the enemy disappears partway through.
    public IEnumerator Perform(EnemyIntentsType type, Action onImpact)
    {
        bool attack = type == EnemyIntentsType.ATTACK;
        bool defend = type == EnemyIntentsType.DEFEND;

        bool landed = false;
        Action impact = () =>
        {
            if (landed) return;
            landed = true;
            if (onImpact != null) onImpact();
        };

        if ((!attack && !defend) || art == null || !isActiveAndEnabled)
        {
            impact();
            yield break;
        }

        StopBeat();
        _performing = true;

        EnemyDataSO data = Data;
        Sprite[] windup = data == null ? null : (attack ? data.attackWindupFrames : data.defendWindupFrames);
        Sprite[] move = data == null ? null : (attack ? data.attackFrames : data.defendFrames);
        _auraColor = attack ? attackAura : defendAura;

        yield return Windup(windup);
        if (art != null) yield return Telegraph();
        if (art != null) yield return attack ? Strike(move, impact) : Guard(move, impact);
        if (art != null) yield return Settle();

        impact();
        _performing = false;
    }

    private IEnumerator Windup(Sprite[] frames)
    {
        int n = frames != null ? frames.Length : 0;
        float total = windupFrameTime * Mathf.Max(0, n - 1) + windupHold;
        int shown = -1;

        for (float t = 0f; t < total; t += Time.deltaTime)
        {
            if (art == null) yield break;
            float p = Mathf.Clamp01(t / total);
            float grow = p * p;

            if (n > 0)
            {
                int i = Mathf.Min(n - 1, Mathf.FloorToInt(t / Mathf.Max(0.01f, windupFrameTime)));
                if (i != shown) { SetSprite(frames[i]); shown = i; }
            }
            else
            {
                // No wind-up art: crouch into the move instead.
                float c = Mathf.SmoothStep(0f, 1f, p);
                _scale = new Vector2(1f + 0.03f * c, 1f - 0.05f * c);
            }

            // Coiling up: a tremble and a charge that both build toward the move.
            _tremble = new Vector2(Mathf.Sin(t * 57f), 0.5f * Mathf.Sin(t * 43f + 1.3f)) * (windupTremble * grow);
            _flashColor = _auraColor;
            _flash = 0.22f * grow * (0.8f + 0.2f * Mathf.Sin(t * 26f));
            _glowAlpha = 0.5f * grow;
            _glowScale = 0.9f + 0.2f * p;
            yield return null;
        }

        _tremble = Vector2.zero;
    }

    private IEnumerator Telegraph()
    {
        _ringT = 0f;   // the aura ring bursts outward
        Vector2 from = _scale;
        Color hot = Color.Lerp(_auraColor, Color.white, 0.45f);

        for (float t = 0f; t < telegraphDuration; t += Time.deltaTime)
        {
            if (art == null) yield break;
            float p = Mathf.Clamp01(t / telegraphDuration);
            float pop = Mathf.Sin(p * Mathf.PI);

            _scale = Vector2.Lerp(from, Vector2.one, p) * Mathf.Lerp(1f, telegraphPop, pop);
            _flashColor = hot;
            _flash = Mathf.Lerp(0.65f, 0.2f, p);
            _glowAlpha = Mathf.Lerp(0.8f, 0.45f, p);
            _glowScale = 1.1f + 0.1f * pop;
            yield return null;
        }

        _scale = Vector2.one;
    }

    private IEnumerator Strike(Sprite[] frames, Action impact)
    {
        int n = frames != null ? frames.Length : 0;
        float total = n > 0 ? attackFrameTime * (n - 1) + attackHold : attackDuration;
        int shown = -1;
        float glow0 = _glowAlpha;

        for (float t = 0f; t < total; t += Time.deltaTime)
        {
            if (art == null) yield break;
            float p = Mathf.Clamp01(t / total);

            if (n > 0)
            {
                int i = Mathf.Min(n - 1, Mathf.FloorToInt(t / Mathf.Max(0.01f, attackFrameTime)));
                if (i != shown) { SetSprite(frames[i]); shown = i; }
            }

            float shape = Snap(p, 0.2f);
            _offset = attackLunge * shape;
            _scale = Vector2.one * Mathf.Lerp(1f, attackGrow, shape);
            _flashColor = attackFlash;
            _flash = 0.45f * shape;
            _glowAlpha = glow0 * (1f - p);

            // The blow lands as the lunge reaches the player.
            if (p >= 0.12f) impact();
            yield return null;
        }

        impact();
        _offset = Vector2.zero;
        _scale = Vector2.one;
    }

    private IEnumerator Guard(Sprite[] frames, Action impact)
    {
        int n = frames != null ? frames.Length : 0;
        float total = n > 0 ? defendFrameTime * (n - 1) + defendHold : defendDuration;
        int shown = -1;

        // The guard is up the moment the pose lands.
        if (n > 0) { SetSprite(frames[0]); shown = 0; }
        impact();

        for (float t = 0f; t < total; t += Time.deltaTime)
        {
            if (art == null) yield break;
            float p = Mathf.Clamp01(t / total);

            if (n > 0)
            {
                int i = Mathf.Min(n - 1, Mathf.FloorToInt(t / Mathf.Max(0.01f, defendFrameTime)));
                if (i != shown) { SetSprite(frames[i]); shown = i; }
            }

            float shape = Snap(p, 0.25f);
            _scale = Vector2.one * Mathf.Lerp(1f, defendSwell, shape);
            _flashColor = defendFlash;
            _flash = 0.4f * shape;
            _glowAlpha = 0.6f * (1f - 0.6f * p);
            _glowScale = 1.1f;
            yield return null;
        }

        _scale = Vector2.one;
    }

    private IEnumerator Settle()
    {
        SetSprite(IdleSprite);
        const float duration = 0.25f;
        float flash0 = _flash, glow0 = _glowAlpha;

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            if (art == null) yield break;
            float p = Mathf.Clamp01(t / duration);
            _flash = Mathf.Lerp(flash0, 0f, p);
            _glowAlpha = Mathf.Lerp(glow0, 0f, p);
            yield return null;
        }

        _offset = Vector2.zero;
        _scale = Vector2.one;
        _tremble = Vector2.zero;
        _flash = 0f;
        _glowAlpha = 0f;
    }

    private void SetSprite(Sprite s)
    {
        if (art == null || s == null || art.sprite == s) return;
        art.sprite = s;
        _punch = 1f;   // a quick squash hides the hard cut between poses
    }

    // ---- applying the pose --------------------------------------------------

    private void LateUpdate()
    {
        if (art == null || !_hasRest) return;
        float dt = Time.deltaTime;

        bool acting = _performing || _beat != null;
        _breathe = Mathf.MoveTowards(_breathe, acting ? 0f : 1f, dt * 3f);
        _punch = Mathf.MoveTowards(_punch, 0f, dt * 7f);

        float b = Mathf.Sin(Time.time * breatheRate * 2f * Mathf.PI) * breatheAmount * _breathe;
        float punch = _punch * _punch;
        Vector2 mul = new Vector2(_scale.x * (1f - 0.35f * b) * (1f + 0.03f * punch),
                                  _scale.y * (1f + b) * (1f - 0.05f * punch));

        // Scale about the feet rather than the sprite's centre, so breathing and
        // swelling grow the enemy up off the floor instead of sinking it into it.
        Vector2 feet = FeetAnchor();
        var tr = art.transform;
        tr.localScale = new Vector3(_restScale.x * mul.x, _restScale.y * mul.y, _restScale.z);
        tr.localPosition = _restPos + (Vector3)(_offset + _tremble)
                         + new Vector3(_restScale.x * feet.x * (1f - mul.x),
                                       _restScale.y * feet.y * (1f - mul.y), 0f);

        ApplyFlash();
        UpdateAura(dt);
    }

    // Bottom centre of the resting pose, in sprite units from its pivot. Every
    // animation frame's pivot is set up to put the feet at this same spot.
    private Vector2 FeetAnchor()
    {
        Sprite s = IdleSprite;
        if (s == null) return Vector2.zero;
        Bounds b = s.bounds;
        return new Vector2(b.center.x, b.min.y);
    }

    private void ApplyFlash()
    {
        float amount = Mathf.Clamp01(_flash);
        if (Mathf.Approximately(amount, _appliedFlash) && _flashColor == _appliedFlashColor) return;

        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        art.GetPropertyBlock(_mpb);
        _mpb.SetColor(FlashColorId, _flashColor);
        _mpb.SetFloat(FlashAmountId, amount);
        art.SetPropertyBlock(_mpb);

        _appliedFlash = amount;
        _appliedFlashColor = _flashColor;
    }

    /// World bounds of the resting pose where it stands at rest, ignoring
    /// whichever frame is showing and wherever the current beat has moved it.
    /// Lets UI that sits over the enemy stay put while it animates.
    public bool TryGetRestBounds(out Bounds bounds)
    {
        bounds = default(Bounds);
        Sprite s = IdleSprite;
        if (!_hasRest || art == null || s == null) return false;

        Transform parent = art.transform.parent;
        Matrix4x4 m = (parent != null ? parent.localToWorldMatrix : Matrix4x4.identity)
                    * Matrix4x4.TRS(_restPos, art.transform.localRotation, _restScale);

        Bounds local = s.bounds;
        bounds = new Bounds(m.MultiplyPoint3x4(local.center), Vector3.zero);
        bounds.Encapsulate(m.MultiplyPoint3x4(new Vector3(local.min.x, local.min.y, 0f)));
        bounds.Encapsulate(m.MultiplyPoint3x4(new Vector3(local.min.x, local.max.y, 0f)));
        bounds.Encapsulate(m.MultiplyPoint3x4(new Vector3(local.max.x, local.min.y, 0f)));
        bounds.Encapsulate(m.MultiplyPoint3x4(new Vector3(local.max.x, local.max.y, 0f)));
        return true;
    }

    // ---- aura ---------------------------------------------------------------

    private void UpdateAura(float dt)
    {
        bool ringActive = _ringT >= 0f;
        if (_auraRoot == null && _glowAlpha <= 0.001f && !ringActive) return;

        Bounds rest;
        if (!TryGetRestBounds(out rest)) return;
        EnsureAura();

        // Centred on the body. Its sorting order is what puts it behind the art;
        // pushing it back in depth instead would drop it past the camera's far
        // plane, which the art sits right on.
        _auraRoot.position = new Vector3(rest.center.x, rest.center.y, art.transform.position.z);
        _glow.sortingLayerID = _ring.sortingLayerID = art.sortingLayerID;
        _glow.sortingOrder = _ring.sortingOrder = art.sortingOrder - 1;

        // Taller than wide, like the enemy itself.
        Vector2 size = new Vector2(rest.size.x * 1.35f, rest.size.y * 1.1f);

        Color g = _auraColor;
        g.a = Mathf.Clamp01(_glowAlpha);
        _glow.color = g;
        _glow.enabled = g.a > 0.001f;
        _glow.transform.localScale = new Vector3(size.x * _glowScale, size.y * _glowScale, 1f);

        if (ringActive)
        {
            _ringT += dt / Mathf.Max(0.05f, auraRingDuration);
            float p = Mathf.Clamp01(_ringT);
            float ease = 1f - (1f - p) * (1f - p);
            float s = Mathf.Lerp(0.55f, 1.5f, ease);

            Color r = Color.Lerp(_auraColor, Color.white, 0.35f);
            r.a = (1f - p) * (1f - p) * 0.9f;
            _ring.color = r;
            _ring.enabled = true;
            _ring.transform.localScale = new Vector3(size.x * s, size.y * s, 1f);

            if (_ringT >= 1f) { _ringT = -1f; _ring.enabled = false; }
        }
    }

    private void EnsureAura()
    {
        if (_auraRoot != null) return;

        _auraRoot = new GameObject(name + " Aura").transform;
        _glow = NewAuraLayer("Glow", GlowSprite());
        _ring = NewAuraLayer("Ring", RingSprite());
        _ring.enabled = false;
    }

    private SpriteRenderer NewAuraLayer(string layerName, Sprite sprite)
    {
        var go = new GameObject(layerName);
        go.transform.SetParent(_auraRoot, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        return sr;
    }

    // Both sprites are one unit across, so their scale is their world size.
    private static Sprite GlowSprite()
    {
        if (_glowSprite == null)
            _glowSprite = RadialSprite("EnemyAuraGlow", r => Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f));
        return _glowSprite;
    }

    private static Sprite RingSprite()
    {
        if (_ringSprite == null)
            _ringSprite = RadialSprite("EnemyAuraRing", r =>
            {
                float d = (r - 0.82f) / 0.09f;
                return Mathf.Exp(-d * d) + 0.12f * Mathf.Clamp01(1f - r);
            });
        return _ringSprite;
    }

    private static Sprite RadialSprite(string spriteName, Func<float, float> alphaAt)
    {
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = spriteName,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f;
                float dy = (y + 0.5f) / size * 2f - 1f;
                float a = Mathf.Clamp01(alphaAt(Mathf.Sqrt(dx * dx + dy * dy)));
                px[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }
        tex.SetPixels32(px);
        tex.Apply();

        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        sprite.name = spriteName;
        return sprite;
    }
}

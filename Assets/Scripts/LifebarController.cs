using UnityEngine;

/// <summary>
/// Drives the 3D lifebar built in Blender (lifebarV4.blend).
///
/// The bar has no moving parts: every mesh stays full length and the shaders
/// clip/recolour themselves from a single 0..1 "_Fill" value read off UV.x
/// (0 at the heart, 1 at the tip). This just pushes health/energy into the
/// renderers via a MaterialPropertyBlock, so nothing here dirties the shared
/// material assets and no per-object material copies are created.
///
///   health -> upper tube: red fluid shrinks, the emptied glass frosts and cracks
///   energy -> lower tube: amber fluid + the zig-zag light string, both fading
///             to dead brown as the charge drops
/// </summary>
[ExecuteAlways]
[AddComponentMenu("UI/Lifebar Controller")]
public class LifebarController : MonoBehaviour
{
    [Header("Values (0-1)")]
    [Range(0f, 1f)] public float health = 1f;
    [Range(0f, 1f)] public float energy = 1f;

    [Header("Animation")]
    [Tooltip("Off = snap straight to the target value.")]
    public bool smooth = true;
    [Tooltip("Bar units per second while catching up to the target.")]
    public float fillSpeed = 1.2f;

    [Header("Renderers")]
    [Tooltip("HP_Fluid and HP_Glass.")]
    public Renderer[] healthParts;
    [Tooltip("EN_Fluid, EN_Glass and EN_Filament.")]
    public Renderer[] energyParts;

    static readonly int FillId = Shader.PropertyToID("_Fill");
    static readonly int PartCenterId = Shader.PropertyToID("_PartCenter");

    float shownHealth = -1f;
    float shownEnergy = -1f;
    MaterialPropertyBlock block;

    // ---------------------------------------------------------------- API
    public void SetHealth(float value01) { health = Mathf.Clamp01(value01); }
    public void SetEnergy(float value01) { energy = Mathf.Clamp01(value01); }

    public void SetHealth(float current, float max)
    {
        SetHealth(max <= 0f ? 0f : current / max);
    }

    public void SetEnergy(float current, float max)
    {
        SetEnergy(max <= 0f ? 0f : current / max);
    }

    /// <summary>Jump to the current values without the catch-up animation.</summary>
    public void SnapToTarget()
    {
        shownHealth = health;
        shownEnergy = energy;
        Apply(shownHealth, shownEnergy);
    }

    // ------------------------------------------------------------- unity
    void Reset()
    {
        AutoWire();
        PushPartCenters();
        SnapToTarget();
    }

    void OnEnable()
    {
        if ((healthParts == null || healthParts.Length == 0) &&
            (energyParts == null || energyParts.Length == 0))
            AutoWire();
        PushPartCenters();
        SnapToTarget();
    }

    void OnValidate()
    {
        if (!isActiveAndEnabled) return;
        Apply(Application.isPlaying ? shownHealth : health,
              Application.isPlaying ? shownEnergy : energy);
    }

    void Update()
    {
        float h = health, e = energy;

        if (smooth && Application.isPlaying)
        {
            float step = Mathf.Max(0.0001f, fillSpeed) * Time.deltaTime;
            h = Mathf.MoveTowards(shownHealth, health, step);
            e = Mathf.MoveTowards(shownEnergy, energy, step);
        }

        if (Mathf.Approximately(h, shownHealth) && Mathf.Approximately(e, shownEnergy))
            return;

        shownHealth = h;
        shownEnergy = e;
        Apply(h, e);
    }

    // ------------------------------------------------------------ internal
    void Apply(float h, float e)
    {
        if (block == null) block = new MaterialPropertyBlock();
        Push(healthParts, Mathf.Clamp01(h));
        Push(energyParts, Mathf.Clamp01(e));
    }

    void Push(Renderer[] parts, float fill)
    {
        if (parts == null) return;
        for (int i = 0; i < parts.Length; i++)
        {
            var r = parts[i];
            if (r == null) continue;
            r.GetPropertyBlock(block);
            block.SetFloat(FillId, fill);
            r.SetPropertyBlock(block);
        }
    }

    /// <summary>
    /// Hand every part its own mesh centre, in object space. The outline pass
    /// uses it to tell a part's outside from its inside, so it does not extrude
    /// the black hull through a ring's bore and into the tube behind it.
    ///
    /// Property blocks are not serialised, so this has to run on every load -
    /// hence OnEnable rather than a one-off editor bake.
    /// </summary>
    [ContextMenu("Push part centres")]
    public void PushPartCenters()
    {
        if (block == null) block = new MaterialPropertyBlock();

        foreach (var mf in GetComponentsInChildren<MeshFilter>(true))
        {
            var mesh = mf.sharedMesh;
            var r = mf.GetComponent<Renderer>();
            if (mesh == null || r == null) continue;

            Vector3 c = mesh.bounds.center;
            r.GetPropertyBlock(block);
            block.SetVector(PartCenterId, new Vector4(c.x, c.y, c.z, 0f));
            r.SetPropertyBlock(block);
        }
    }

    /// <summary>Find the parts by the names the Blender export uses.</summary>
    [ContextMenu("Auto wire parts")]
    public void AutoWire()
    {
        var all = GetComponentsInChildren<Renderer>(true);
        var hp = new System.Collections.Generic.List<Renderer>();
        var en = new System.Collections.Generic.List<Renderer>();

        foreach (var r in all)
        {
            string n = r.name;
            if (n.StartsWith("HP_")) hp.Add(r);
            else if (n.StartsWith("EN_") && n != "EN_Cap") en.Add(r);
        }

        healthParts = hp.ToArray();
        energyParts = en.ToArray();
    }
}

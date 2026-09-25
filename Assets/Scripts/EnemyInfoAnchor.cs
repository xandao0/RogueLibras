using UnityEngine;

// Pins the enemy's health bar, name and intent above the enemy art itself
// instead of to a fixed spot on the screen.
//
// The canvas this sits on is World Space on purpose: as a Screen Space overlay
// it ignored its parent entirely, so it never lined up with the enemy and the
// Scene view showed it somewhere else again. In world space what you arrange in
// the Scene view is what the Game view renders, and because the panel tracks the
// sprite's rendered bounds it stays correct for enemies of different sizes and
// follows the art wherever it goes, parallax drift included.
[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class EnemyInfoAnchor : MonoBehaviour
{
    [Tooltip("The enemy art this panel sits above.")]
    [SerializeField] private SpriteRenderer art;

    [Tooltip("The child that should end up just above the enemy -- normally the " +
             "health bar. Everything else keeps whatever offset it already has " +
             "from this one, so the panel's layout never has to be known here.")]
    [SerializeField] private RectTransform anchorChild;

    [Tooltip("World size of one canvas unit. 0.58 reproduces the size the panel " +
             "had while it was a screen-space overlay.")]
    [SerializeField] private float worldScale = 0.58f;

    [Tooltip("World-space gap between the top of the enemy art and the anchor child.")]
    [SerializeField] private float verticalPadding = 22f;

    [Tooltip("Extra nudge in world units, if the art's bounds aren't centred on " +
             "the character (lots of sprites carry empty margins).")]
    [SerializeField] private Vector2 offset;

    private RectTransform _rt;
    private EnemyReaction _reaction;

    private EnemyReaction Reaction
    {
        get
        {
            if (_reaction == null && art != null) _reaction = art.GetComponentInParent<EnemyReaction>();
            return _reaction;
        }
    }

    private void OnEnable()
    {
        _rt = (RectTransform)transform;
        Apply();
    }

    private void LateUpdate()
    {
        Apply();
    }

    private void Apply()
    {
        if (art == null || anchorChild == null) return;
        if (_rt == null) _rt = (RectTransform)transform;

        // The spawn point is scaled non-uniformly, so cancel the parent out and
        // leave the panel square in world units however the enemy is stretched.
        Vector3 parent = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        _rt.localScale = new Vector3(SafeDivide(worldScale, parent.x),
                                     SafeDivide(worldScale, parent.y),
                                     1f);

        // Scale first, then shift the whole panel by however far the anchor child
        // is from where it belongs. The child's offset from the panel is fixed
        // once the scale is set, so this lands exactly in one step.
        //
        // While the enemy animates, its frames have different outlines and it
        // lunges and breathes; tracking those bounds would bounce the health bar
        // around with it. The resting pose's bounds stay put.
        Bounds b;
        if (!Application.isPlaying || Reaction == null || !Reaction.TryGetRestBounds(out b))
            b = art.bounds;
        float wantX = b.center.x + offset.x;
        float wantY = b.max.y + verticalPadding + offset.y;

        Vector3 have = anchorChild.position;
        _rt.position += new Vector3(wantX - have.x, wantY - have.y, 0f);
    }

    private static float SafeDivide(float value, float by)
    {
        return Mathf.Abs(by) > 0.0001f ? value / by : value;
    }
}

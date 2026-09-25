using System.Collections.Generic;
using UnityEngine;

// Arranges the cards in a hand container into a fan (arc), smoothly animating
// toward their target position/rotation/scale every frame. The currently
// selected card (CardManager.instance.selectedCard) is popped up, enlarged,
// and brought to the front; the same happens for a card the pointer has
// held over for a moment (see CardDisplay.IsHoverHeld). Every other card in
// the hand is greyed out while a selection or held-hover is active. The
// border shine only shows on the actually-selected card.
[RequireComponent(typeof(RectTransform))]
public class CardFanLayout : MonoBehaviour
{
    [Header("Fan Shape")]
    [SerializeField] private float cardSpacing = 250f;
    [SerializeField] private float maxSpreadWidth = 1340f;
    [SerializeField] private float maxFanAngle = 26f;
    [SerializeField] private float arcHeight = 50f;

    // Raised cards are drawn upright and large enough to actually read the
    // description and signs on; the lift is absolute, not added to the arc.
    [Header("Selected Card")]
    [SerializeField] private float selectedScale = 2.25f;
    [SerializeField] private float selectedYOffset = 170f;
    [SerializeField] private float normalScale = 1.45f;

    [Header("Hover")]
    [SerializeField] private float hoverYOffset = 120f;
    [SerializeField] private float hoverScale = 2f;

    [Header("Animation")]
    [SerializeField] private float followSpeed = 12f;
    [Tooltip("Leave at 1. Fading a card lets the bright scene behind it wash " +
             "through, which reads as washed-out grey rather than unselected - " +
             "CardDisplay's dark tint overlay is what dims the rest of the hand.")]
    [Range(0f, 1f)] [SerializeField] private float greyedOutAlpha = 1f;

    private readonly List<Transform> _childrenBuffer = new List<Transform>();

    private void LateUpdate()
    {
        int count = transform.childCount;
        if (count == 0) return;

        CardDisplay selected = CardManager.instance != null ? CardManager.instance.selectedCard : null;

        _childrenBuffer.Clear();
        for (int i = 0; i < count; i++) _childrenBuffer.Add(transform.GetChild(i));

        // Bringing the hovered/selected card to the front (SetAsLastSibling) changes
        // sibling order, which would otherwise reshuffle everyone else's fan slot on
        // every hover change. Sort by each card's current on-screen X instead, so the
        // hand's left-to-right order stays stable no matter what got reordered to front.
        _childrenBuffer.Sort(CompareByCurrentX);

        // Is anything highlighted at all (selected, or hovered long enough to count)?
        // That's what decides whether the REST of the hand greys out.
        bool anyHighlighted = selected != null;
        if (!anyHighlighted)
        {
            for (int i = 0; i < _childrenBuffer.Count; i++)
            {
                var cd = _childrenBuffer[i].GetComponent<CardDisplay>();
                if (cd != null && cd.IsHoverHeld) { anyHighlighted = true; break; }
            }
        }

        float dt = Time.unscaledDeltaTime * followSpeed;
        Transform toBringToFront = null;

        for (int i = 0; i < _childrenBuffer.Count; i++)
        {
            Transform child = _childrenBuffer[i];
            RectTransform rt = child as RectTransform;
            if (rt == null) continue;

            // The old HorizontalLayoutGroup anchored cards to the container's
            // bottom-left corner. Force a centered point-anchor so anchoredPosition
            // (0,0) means "container center", which is what the fan math assumes.
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            CardDisplay cd = child.GetComponent<CardDisplay>();
            bool isSelected = selected != null && cd == selected;
            bool isHoverHeld = cd != null && cd.IsHoverHeld && !isSelected;

            float t = count == 1 ? 0.5f : (float)i / (count - 1);
            float centered = t - 0.5f;

            float spacing = Mathf.Min(cardSpacing, maxSpreadWidth / Mathf.Max(1, count - 1));
            float xPos = centered * spacing * (count - 1);
            float yPos = -Mathf.Abs(centered) * 2f * arcHeight;
            float zRot = -centered * maxFanAngle;

            Vector2 targetPos;
            Quaternion targetRot;
            float targetScale;

            // A raised card is there to be read, so it leaves the fan entirely:
            // upright, and lifted by a fixed amount rather than following the
            // arc - otherwise a card at the end of the hand sits low enough that
            // enlarging it pushes its bottom off screen.
            if (isSelected)
            {
                targetPos = new Vector2(xPos, selectedYOffset);
                targetRot = Quaternion.identity;
                targetScale = selectedScale;
                toBringToFront = child;
            }
            else if (isHoverHeld)
            {
                targetPos = new Vector2(xPos, hoverYOffset);
                targetRot = Quaternion.identity;
                targetScale = hoverScale;
                toBringToFront = toBringToFront == null ? child : toBringToFront;
            }
            else
            {
                targetPos = new Vector2(xPos, yPos);
                targetRot = Quaternion.Euler(0f, 0f, zRot);
                targetScale = normalScale;
            }

            rt.anchoredPosition = Vector2.Lerp(rt.anchoredPosition, targetPos, dt);
            rt.localRotation = Quaternion.Slerp(rt.localRotation, targetRot, dt);
            rt.localScale = Vector3.Lerp(rt.localScale, Vector3.one * targetScale, dt);

            bool dimmed = anyHighlighted && !isSelected && !isHoverHeld;

            CanvasGroup cg = child.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                float targetAlpha = dimmed ? greyedOutAlpha : 1f;
                cg.alpha = Mathf.Lerp(cg.alpha, targetAlpha, dt);
            }

            // The tint does all the dimming: it darkens the card itself, where
            // fading would instead thin it out and let the scene bleed through.
            if (cd != null) cd.StepDim(dimmed ? 1f : 0f, dt);

            Transform shine = child.Find("Shine");
            if (shine != null && shine.gameObject.activeSelf != isSelected)
            {
                shine.gameObject.SetActive(isSelected);
            }
        }

        if (toBringToFront != null) toBringToFront.SetAsLastSibling();
    }

    private static int CompareByCurrentX(Transform a, Transform b)
    {
        var ra = a as RectTransform;
        var rb = b as RectTransform;
        float xa = ra != null ? ra.anchoredPosition.x : 0f;
        float xb = rb != null ? rb.anchoredPosition.x : 0f;
        return xa.CompareTo(xb);
    }
}

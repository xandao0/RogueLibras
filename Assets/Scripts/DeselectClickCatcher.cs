using UnityEngine;
using UnityEngine.EventSystems;

// Sits as the first (furthest back) child of the hand's canvas, covering the
// whole screen with an invisible raycast target. Any click that isn't
// absorbed by a card or button on top of it lands here and clears the
// current card selection.
public class DeselectClickCatcher : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        // With Ajuda open, a click off the card only leaves help mode; the card
        // stays raised so it can still be played.
        if (CardHelp.instance != null && CardHelp.instance.CloseFromOutsideClick()) return;

        if (CardManager.instance != null) CardManager.instance.selectedCard = null;
    }
}

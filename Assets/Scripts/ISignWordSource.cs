using System.Collections.Generic;

// Something on screen besides the cards that shows words or numbers which can
// turn into SignWriting (the enemy's name and health, the block counter).
// CardManager offers these to SignProgression along with the hand at the start
// of every turn, and asks them to redraw once a word has changed.
public interface ISignWordSource
{
    /// Every word currently on screen that has a sign to graduate to.
    void CollectSignWords(List<string> into);

    /// Redraw the words at whatever stage they are now.
    void RefreshSignText();
}

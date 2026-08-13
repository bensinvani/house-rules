using System.Collections;
using HouseRules.Blackjack;

namespace HouseRules.Blackjack.Presentation
{
    /// <summary>
    /// Turns one engine event into motion. The sequencer waits for the returned
    /// coroutine to finish before presenting the next event, so an implementation
    /// controls pacing simply by taking as long as it needs.
    /// </summary>
    public interface IEventPresenter
    {
        IEnumerator Present(GameEvent gameEvent);
    }
}

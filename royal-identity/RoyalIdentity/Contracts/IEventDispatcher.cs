using RoyalIdentity.Events;

namespace RoyalIdentity.Contracts;

public interface IEventDispatcher
{
    /// <summary>
    /// Raises the specified event.
    /// </summary>
    /// <param name="evt">The event.</param>
    ValueTask DispatchAsync(Event evt);
}

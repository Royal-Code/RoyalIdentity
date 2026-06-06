using RoyalIdentity.Events;
using RoyalIdentity.Models;

namespace RoyalIdentity.Contracts;

public interface IEventDispatcher
{
    /// <summary>
    /// Raises the specified event.
    /// </summary>
    /// <param name="evt">The event.</param>
    ValueTask DispatchAsync(Event evt);

    /// <summary>
    /// Raises the specified event scoped to a realm.
    /// Sets <see cref="Event.RealmId"/> before dispatching.
    /// </summary>
    ValueTask DispatchAsync(Event evt, Realm realm);
}

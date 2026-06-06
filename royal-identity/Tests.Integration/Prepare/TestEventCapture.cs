using RoyalIdentity.Contracts;
using RoyalIdentity.Events;
using RoyalIdentityRealm = RoyalIdentity.Models.Realm;

namespace Tests.Integration.Prepare;

/// <summary>
/// Captures all events dispatched through the event dispatcher for test assertions.
/// </summary>
public class TestEventCapture
{
    public List<Event> Events { get; } = [];

    public void Add(Event evt) => Events.Add(evt);

    public void Reset() => Events.Clear();
}

/// <summary>
/// IEventDispatcher decorator that captures all dispatched events into TestEventCapture.
/// </summary>
public class CapturingEventDispatcher(IEventDispatcher inner, TestEventCapture capture) : IEventDispatcher
{
    public ValueTask DispatchAsync(Event evt)
    {
        capture.Add(evt);
        return inner.DispatchAsync(evt);
    }

    public ValueTask DispatchAsync(Event evt, RoyalIdentityRealm realm)
    {
        evt.RealmId = realm.Id;
        capture.Add(evt);
        return inner.DispatchAsync(evt);
    }
}

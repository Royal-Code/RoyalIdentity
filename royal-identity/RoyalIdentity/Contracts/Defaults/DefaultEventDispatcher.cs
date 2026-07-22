using RoyalIdentity.Configuration;
using RoyalIdentity.Events;
using RoyalIdentity.Models;

namespace RoyalIdentity.Contracts.Defaults;

public class DefaultEventDispatcher : IEventDispatcher
{
    private readonly IConfigurationSnapshot snapshot;
    private readonly IServiceProvider sp;

    public DefaultEventDispatcher(IConfigurationSnapshot snapshot, IServiceProvider sp)
    {
        this.snapshot = snapshot;
        this.sp = sp;
    }

    public async ValueTask DispatchAsync(Event evt)
    {
        if (!snapshot.ServerOptions.DispatchEvents)
            return;

        await DispatchCoreAsync(evt);
    }

    public async ValueTask DispatchAsync(Event evt, Realm realm)
    {
        evt.RealmId = realm.Id;

        if (!realm.Options.DispatchEvents)
            return;

        await DispatchCoreAsync(evt);
    }

    private async ValueTask DispatchCoreAsync(Event evt)
    {
        var type = typeof(DefaultEventDispatcher<>).MakeGenericType(evt.GetType());
        var dispatcher = (IEventDispatcher)sp.GetService(type)!;
        await dispatcher.DispatchAsync(evt);
    }
}

internal class DefaultEventDispatcher<TEvent>(IEnumerable<IEventObserver<TEvent>> observers) : IEventDispatcher
    where TEvent : Event
{
    public async ValueTask DispatchAsync(Event evt)
    {
        var e = (TEvent)evt;
        foreach (var observer in observers)
            await observer.HandleAsync(e);
    }

    public ValueTask DispatchAsync(Event evt, Realm realm)
    {
        evt.RealmId = realm.Id;
        return DispatchAsync(evt);
    }
}

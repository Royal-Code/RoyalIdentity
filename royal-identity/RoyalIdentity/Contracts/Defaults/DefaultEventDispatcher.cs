using Microsoft.Extensions.Options;
using RoyalIdentity.Events;
using RoyalIdentity.Options;

namespace RoyalIdentity.Contracts.Defaults;

public class DefaultEventDispatcher : IEventDispatcher
{
    private readonly ServerOptions options;
    private readonly IServiceProvider sp;

    public DefaultEventDispatcher(IOptions<ServerOptions> options, IServiceProvider sp)
    {
        this.options = options.Value;
        this.sp = sp;
    }

    public async ValueTask DispatchAsync(Event evt)
    {
        if (!options.DispatchEvents)
            return;

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
}

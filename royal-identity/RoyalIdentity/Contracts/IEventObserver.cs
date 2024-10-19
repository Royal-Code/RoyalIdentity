using RoyalIdentity.Events;

namespace RoyalIdentity.Contracts;

public interface IEventObserver<in TEvent>
    where TEvent : Event
{
    Task HandleAsync(TEvent evt);
}
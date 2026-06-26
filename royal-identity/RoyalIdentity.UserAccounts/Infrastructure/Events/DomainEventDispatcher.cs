using RoyalCode.DomainEvents;

namespace RoyalIdentity.UserAccounts.Infrastructure.Events;

/// <summary>
/// Default in-process <see cref="IDomainEventDispatcher"/>: fans each committed event to every registered
/// <see cref="IDomainEventObserver"/>, in registration order. A no-op when there are no observers (the events were
/// already cleared from the aggregates by the collector).
/// </summary>
public sealed class DomainEventDispatcher(IEnumerable<IDomainEventObserver> observers) : IDomainEventDispatcher
{
    private readonly IReadOnlyList<IDomainEventObserver> observers = observers.ToList();

    /// <inheritdoc />
    public async Task DispatchAsync(IReadOnlyList<IDomainEvent> domainEvents, CancellationToken ct = default)
    {
        if (observers.Count == 0 || domainEvents.Count == 0)
        {
            return;
        }

        foreach (var domainEvent in domainEvents)
        {
            foreach (var observer in observers)
            {
                await observer.OnEventAsync(domainEvent, ct);
            }
        }
    }
}

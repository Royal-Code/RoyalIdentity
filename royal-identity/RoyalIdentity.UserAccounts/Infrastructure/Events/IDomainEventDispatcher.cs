using RoyalCode.DomainEvents;

namespace RoyalIdentity.UserAccounts.Infrastructure.Events;

/// <summary>
/// Dispatches committed domain events to the registered <see cref="IDomainEventObserver"/> set. The module's
/// <c>DbContext</c> collects the aggregates' events, commits, and then calls this dispatcher (post-commit — Q8/§10).
/// </summary>
public interface IDomainEventDispatcher
{
    /// <summary>
    /// Dispatches the committed events, in order, to every observer.
    /// </summary>
    /// <param name="domainEvents">The events collected before the commit.</param>
    /// <param name="ct">The cancellation token.</param>
    Task DispatchAsync(IReadOnlyList<IDomainEvent> domainEvents, CancellationToken ct = default);
}

using RoyalCode.DomainEvents;

namespace RoyalIdentity.UserAccounts.Infrastructure.Events;

/// <summary>
/// Observes domain events emitted by the module's aggregates, dispatched <b>after</b> the unit of work commits
/// (ADR-017 §2.11 / Q8). Implementations are registered in DI; the module's <see cref="IDomainEventDispatcher"/> fans
/// each committed event to every observer. Observers must not assume the event carries secrets — the raw action token
/// never enters an event (invariant).
/// </summary>
public interface IDomainEventObserver
{
    /// <summary>
    /// Handles a committed domain event.
    /// </summary>
    /// <param name="domainEvent">The committed domain event.</param>
    /// <param name="ct">The cancellation token.</param>
    Task OnEventAsync(IDomainEvent domainEvent, CancellationToken ct = default);
}

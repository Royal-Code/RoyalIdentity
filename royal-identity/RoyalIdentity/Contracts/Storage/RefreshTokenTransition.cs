using RoyalIdentity.Models.Tokens;

namespace RoyalIdentity.Contracts.Storage;

/// <summary>
/// Outcome of a conditional refresh-token transition (MP-3 / plan-data-operational-storage DF12).
/// </summary>
public enum RefreshTokenTransitionOutcome
{
    /// <summary>The token does not exist (or no longer does).</summary>
    NotFound = 0,

    /// <summary>This caller made the transition — exactly one concurrent caller can observe it.</summary>
    Succeeded = 1,

    /// <summary>
    /// The token was already in the target state when the condition was evaluated. Only this outcome may be
    /// submitted to the post-consumption tolerance policy, and only over the rematerialized state.
    /// </summary>
    AlreadyConsumed = 2,

    /// <summary>
    /// The expected state version no longer matched: another writer moved the token first. A conflict is
    /// never a silent success.
    /// </summary>
    Conflict = 3
}

/// <summary>
/// Result of a conditional refresh-token transition (DF12). <see cref="Current"/> carries the rematerialized
/// token when the store could read it, which is what a caller applying the tolerance policy must look at —
/// never the instance it already mutated.
/// </summary>
/// <param name="Outcome">What happened to the transition.</param>
/// <param name="Current">The rematerialized token, or <c>null</c> when there is none to report.</param>
public sealed record RefreshTokenTransition(RefreshTokenTransitionOutcome Outcome, RefreshToken? Current)
{
    /// <summary>The token does not exist.</summary>
    public static RefreshTokenTransition NotFound() => new(RefreshTokenTransitionOutcome.NotFound, null);

    /// <summary>This caller won the transition.</summary>
    public static RefreshTokenTransition Succeeded(RefreshToken current)
        => new(RefreshTokenTransitionOutcome.Succeeded, current);

    /// <summary>The token was already consumed; <paramref name="current"/> is the rematerialized state.</summary>
    public static RefreshTokenTransition AlreadyConsumed(RefreshToken current)
        => new(RefreshTokenTransitionOutcome.AlreadyConsumed, current);

    /// <summary>Another writer moved the token first.</summary>
    public static RefreshTokenTransition Conflict(RefreshToken? current)
        => new(RefreshTokenTransitionOutcome.Conflict, current);

    /// <summary>Whether this caller made the transition.</summary>
    public bool IsSuccess => Outcome is RefreshTokenTransitionOutcome.Succeeded;
}

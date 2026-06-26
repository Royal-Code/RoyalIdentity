namespace RoyalIdentity.Users;

/// <summary>
/// Current security-sensitive state of an account, read through the optional <c>IUserSecurityStateProvider</c>
/// capability (ADR-017 §2.10/§2.13, Q15). Primitives/BCL only — the core never sees the module's domain types.
/// </summary>
/// <param name="SecurityStamp">
/// The opaque security stamp, captured at sign-in for cookie/claims revalidation and traceability.
/// </param>
/// <param name="SessionsValidAfter">
/// The point after which sessions remain valid; a session started before it is invalid when the realm enforces
/// passive invalidation. The provider returns it only when the realm enforces it (the integration gates this by the
/// realm policy); <c>null</c> means the core does not enforce <c>SessionsValidAfter</c> for this subject.
/// </param>
public sealed record UserSecurityState(string? SecurityStamp, DateTimeOffset? SessionsValidAfter);

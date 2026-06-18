namespace RoyalIdentity.Users;

/// <summary>
/// Lean edge representation of an authenticated subject (user), holding only what the IdP needs
/// protocolarmente per request. Replaces the rich <c>IdentityUser</c> (removed); the rich account model
/// lives in the future RoyalIdentity.UserAccounts module (ADR-013/014).
/// </summary>
/// <param name="SubjectId">Stable, immutable identifier — the OIDC <c>sub</c>. Never derived from username.</param>
/// <param name="DisplayName">Human-friendly name — the OIDC <c>name</c>.</param>
/// <param name="IsActive">Whether the account behind the subject is active.</param>
public sealed record Subject(string SubjectId, string DisplayName, bool IsActive);

using RoyalIdentity.UserAccounts.Features.Accounts.Domain;

namespace RoyalIdentity.UserAccounts.Features.Accounts.UseCases;

/// <summary>
/// A short-lived action-token challenge issued after a valid credential is gated by a required password change.
/// The token is raw delivery material: use it once and never persist/log it outside the action token hash store.
/// </summary>
/// <param name="SubjectId">The subject that must change its password.</param>
/// <param name="DisplayName">The subject display name.</param>
/// <param name="Token">The raw action token.</param>
/// <param name="ExpiresAt">When the token expires.</param>
/// <param name="RequiredAction">The required action that triggered the challenge.</param>
public sealed record ChangeExpiredPasswordToken(
	string SubjectId,
	string DisplayName,
	string Token,
	DateTimeOffset ExpiresAt,
	LocalRequiredAction RequiredAction);

namespace RoyalIdentity.UserAccounts.Options;

/// <summary>
/// Composable session/token invalidation effects (ADR-017 §2.7). The flags express the
/// "current/other/all sessions × refresh tokens" matrix without preset explosion;
/// <see cref="SessionInvalidationPresets"/> exposes the named combinations for configuration/UI.
/// </summary>
[Flags]
public enum SessionInvalidation
{
	/// <summary>No invalidation.</summary>
	None = 0,

	/// <summary>The current session (the one that triggered the change).</summary>
	CurrentSession = 1,

	/// <summary>All sessions except the current one.</summary>
	OtherSessions = 2,

	/// <summary>All interactive sessions (current and others).</summary>
	AllSessions = CurrentSession | OtherSessions,

	/// <summary>Refresh tokens of the subject.</summary>
	RefreshTokens = 4
}

/// <summary>
/// Named invalidation presets (ADR-017 §2.7), mapped to <see cref="SessionInvalidation"/> flags.
/// </summary>
public static class SessionInvalidationPresets
{
	/// <summary>Revoke other sessions, keep the current one.</summary>
	public const SessionInvalidation KeepCurrentSessionOnly = SessionInvalidation.OtherSessions;

	/// <summary>Revoke other sessions, keep the current one.</summary>
	public const SessionInvalidation RevokeOtherSessions = SessionInvalidation.OtherSessions;

	/// <summary>Revoke all interactive sessions.</summary>
	public const SessionInvalidation RevokeAllSessions = SessionInvalidation.AllSessions;

	/// <summary>Revoke all interactive sessions and refresh tokens.</summary>
	public const SessionInvalidation RevokeAllSessionsAndRefreshTokens =
		SessionInvalidation.AllSessions | SessionInvalidation.RefreshTokens;
}

/// <summary>
/// Security audit categories (ADR-017 §2.11). Auditing is configured by category, not a fixed event list;
/// the security categories are on by default and the rest is off.
/// </summary>
[Flags]
public enum SecurityAuditCategories
{
	/// <summary>Audit nothing.</summary>
	None = 0,

	/// <summary>Password set/change/reset and credential changes.</summary>
	Credential = 1,

	/// <summary>Password recovery requests and resets.</summary>
	Recovery = 2,

	/// <summary>Email/phone verification.</summary>
	Verification = 4,

	/// <summary>Lockout start/end/reset.</summary>
	Lockout = 8,

	/// <summary>Administrative security actions (admin reset, block/unblock, stamp regeneration).</summary>
	AdminSecurity = 16,

	/// <summary>Session/refresh-token revocation.</summary>
	SessionRevocation = 32,

	/// <summary>Failed authentication attempts.</summary>
	AuthenticationFailure = 64,

	/// <summary>All security categories.</summary>
	All = Credential | Recovery | Verification | Lockout | AdminSecurity | SessionRevocation | AuthenticationFailure
}

/// <summary>
/// Realm-scoped account security lifecycle policies owned by the UserAccounts module (ADR-017 §2.13).
/// Per-credential-trigger active revocation policy (Q7), action-token lifetimes and audit categories live here as a
/// single-owner block. The <b>session lifecycle</b> (SSO expiration/idle and the passive <c>SessionsValidAfter</c>
/// enforcement gate) is an IdP concern and lives in the core <c>RealmOptions.Session</c> (ADR-017 §2.12, amended).
/// </summary>
public class SecurityLifecycleOptions
{
	/// <summary>
	/// Creates a new instance with the decided per-trigger defaults (ADR-017 §2.7).
	/// </summary>
	public SecurityLifecycleOptions()
	{
	}

	/// <summary>
	/// Creates an independent copy of another instance.
	/// </summary>
	public SecurityLifecycleOptions(SecurityLifecycleOptions other)
	{
		OnVoluntaryPasswordChange = other.OnVoluntaryPasswordChange;
		OnPasswordRecoveryReset = other.OnPasswordRecoveryReset;
		OnAdminPasswordReset = other.OnAdminPasswordReset;
		OnAdminMustChangePassword = other.OnAdminMustChangePassword;
		OnSensitiveProfileChange = other.OnSensitiveProfileChange;
		PasswordRecoveryTokenLifetimeMinutes = other.PasswordRecoveryTokenLifetimeMinutes;
		PasswordRecoveryResendCooldownSeconds = other.PasswordRecoveryResendCooldownSeconds;
		ChangeExpiredPasswordTokenLifetimeMinutes = other.ChangeExpiredPasswordTokenLifetimeMinutes;
		EmailVerificationTokenLifetimeMinutes = other.EmailVerificationTokenLifetimeMinutes;
		PhoneVerificationTokenLifetimeMinutes = other.PhoneVerificationTokenLifetimeMinutes;
		AuditCategories = other.AuditCategories;
	}

	/// <summary>
	/// Gets or sets the active revocation applied on a voluntary password change. Default: keep all sessions
	/// (revoke others only if the policy asks).
	/// </summary>
	public SessionInvalidation OnVoluntaryPasswordChange { get; set; } = SessionInvalidation.None;

	/// <summary>
	/// Gets or sets the active revocation applied on a password reset via recovery. Default: revoke all sessions
	/// and refresh tokens.
	/// </summary>
	public SessionInvalidation OnPasswordRecoveryReset { get; set; } =
		SessionInvalidationPresets.RevokeAllSessionsAndRefreshTokens;

	/// <summary>
	/// Gets or sets the active revocation applied on an administrative password set/reset. Default: revoke all
	/// sessions and refresh tokens.
	/// </summary>
	public SessionInvalidation OnAdminPasswordReset { get; set; } =
		SessionInvalidationPresets.RevokeAllSessionsAndRefreshTokens;

	/// <summary>
	/// Gets or sets the active revocation applied when an administrator sets <c>MustChangePassword</c>.
	/// Default: none (opt-in per realm).
	/// </summary>
	public SessionInvalidation OnAdminMustChangePassword { get; set; } = SessionInvalidation.None;

	/// <summary>
	/// Gets or sets the active revocation applied on sensitive profile changes (email/phone/roles).
	/// Default: none (opt-in per realm).
	/// </summary>
	public SessionInvalidation OnSensitiveProfileChange { get; set; } = SessionInvalidation.None;

	/// <summary>
	/// Gets or sets the lifetime, in minutes, of a password recovery action token (the mandatory TTL — ADR-017
	/// §2.4). Must be greater than zero.
	/// </summary>
	public int PasswordRecoveryTokenLifetimeMinutes { get; set; } = 60;

	/// <summary>
	/// Gets or sets the minimum interval, in seconds, between password recovery emissions for the same account
	/// (a per-realm resend throttle). Zero disables the throttle (each request re-issues and revokes the previous
	/// token). IP/identifier-scoped rate limiting is an edge concern handled by the HTTP layer.
	/// </summary>
	public int PasswordRecoveryResendCooldownSeconds { get; set; } = 0;

	/// <summary>
	/// Gets or sets the lifetime, in minutes, of a forced/expired password-change action token. Must be greater
	/// than zero.
	/// </summary>
	public int ChangeExpiredPasswordTokenLifetimeMinutes { get; set; } = 10;

	/// <summary>
	/// Gets or sets the lifetime, in minutes, of an email verification action token (TTL — ADR-017 §2.4/§2.8).
	/// Must be greater than zero.
	/// </summary>
	public int EmailVerificationTokenLifetimeMinutes { get; set; } = 1440;

	/// <summary>
	/// Gets or sets the lifetime, in minutes, of a phone verification action token (TTL — ADR-017 §2.4/§2.8).
	/// Must be greater than zero.
	/// </summary>
	public int PhoneVerificationTokenLifetimeMinutes { get; set; } = 15;

	/// <summary>
	/// Gets or sets the audit categories enabled for this realm. Default: all security categories on.
	/// </summary>
	public SecurityAuditCategories AuditCategories { get; set; } = SecurityAuditCategories.All;

	/// <summary>
	/// Validates internal consistency of the lifecycle options.
	/// </summary>
	/// <returns>A list of configuration errors. Empty means valid.</returns>
	public IReadOnlyList<string> Validate()
	{
		List<string> errors = [];

		if (PasswordRecoveryTokenLifetimeMinutes <= 0)
		{
			errors.Add("SecurityLifecycle.PasswordRecoveryTokenLifetimeMinutes must be greater than zero.");
		}

		if (PasswordRecoveryResendCooldownSeconds < 0)
		{
			errors.Add("SecurityLifecycle.PasswordRecoveryResendCooldownSeconds cannot be negative.");
		}

		if (ChangeExpiredPasswordTokenLifetimeMinutes <= 0)
		{
			errors.Add("SecurityLifecycle.ChangeExpiredPasswordTokenLifetimeMinutes must be greater than zero.");
		}

		if (EmailVerificationTokenLifetimeMinutes <= 0)
		{
			errors.Add("SecurityLifecycle.EmailVerificationTokenLifetimeMinutes must be greater than zero.");
		}

		if (PhoneVerificationTokenLifetimeMinutes <= 0)
		{
			errors.Add("SecurityLifecycle.PhoneVerificationTokenLifetimeMinutes must be greater than zero.");
		}

		return errors;
	}
}

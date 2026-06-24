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
/// Invalidation, SSO session lifetime and audit categories live here as a single-owner block.
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
		EnableSessionInvalidationByState = other.EnableSessionInvalidationByState;
		EnableSsoSessionExpiration = other.EnableSsoSessionExpiration;
		SsoSessionMaxMinutes = other.SsoSessionMaxMinutes;
		SsoSessionIdleMinutes = other.SsoSessionIdleMinutes;
		IdleTouchIntervalMinutes = other.IdleTouchIntervalMinutes;
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
	/// Gets or sets whether sessions are validated passively by <c>SessionsValidAfter</c> (a session started
	/// before the marker is invalid). Default off preserves current behavior. When on, the user provider must
	/// expose the security-state capability — see <see cref="RequiresSecurityStateProvider"/>.
	/// </summary>
	public bool EnableSessionInvalidationByState { get; set; } = false;

	/// <summary>
	/// Gets or sets whether the realm enforces an SSO session lifetime (gives behavior to
	/// <c>UserSession.ExpiresAt</c>, Realm-only — ADR-017 §2.12). Default off preserves today's reserved field.
	/// </summary>
	public bool EnableSsoSessionExpiration { get; set; } = false;

	/// <summary>
	/// Gets or sets the SSO session maximum lifetime, in minutes, used when
	/// <see cref="EnableSsoSessionExpiration"/> is on.
	/// </summary>
	public int SsoSessionMaxMinutes { get; set; } = 600;

	/// <summary>
	/// Gets or sets the SSO session idle timeout, in minutes, used when <see cref="EnableSsoSessionExpiration"/>
	/// is on. Zero disables the idle timeout (only the max lifetime applies).
	/// </summary>
	public int SsoSessionIdleMinutes { get; set; } = 0;

	/// <summary>
	/// Gets or sets the minimum window, in minutes, between idle touches of <c>LastSeenAt</c>. Throttles writes:
	/// the store only updates when the last touch is older than this window.
	/// </summary>
	public int IdleTouchIntervalMinutes { get; set; } = 5;

	/// <summary>
	/// Gets or sets the audit categories enabled for this realm. Default: all security categories on.
	/// </summary>
	public SecurityAuditCategories AuditCategories { get; set; } = SecurityAuditCategories.All;

	/// <summary>
	/// Gets whether this realm requires a security-state provider capability (ADR-017 §2.13 / Q15). When true and
	/// the user provider does not expose <c>IUserSecurityStateProvider</c>, the composition is a configuration
	/// error (enforced by the integration, where the core port is known).
	/// </summary>
	public bool RequiresSecurityStateProvider => EnableSessionInvalidationByState;

	/// <summary>
	/// Validates internal consistency of the lifecycle options.
	/// </summary>
	/// <returns>A list of configuration errors. Empty means valid.</returns>
	public IReadOnlyList<string> Validate()
	{
		List<string> errors = [];

		if (EnableSsoSessionExpiration)
		{
			if (SsoSessionMaxMinutes <= 0)
			{
				errors.Add("SecurityLifecycle.SsoSessionMaxMinutes must be greater than zero when SSO session expiration is enabled.");
			}

			if (SsoSessionIdleMinutes < 0)
			{
				errors.Add("SecurityLifecycle.SsoSessionIdleMinutes cannot be negative.");
			}

			if (SsoSessionIdleMinutes > 0 && SsoSessionMaxMinutes > 0 && SsoSessionIdleMinutes > SsoSessionMaxMinutes)
			{
				errors.Add("SecurityLifecycle.SsoSessionIdleMinutes cannot be greater than SsoSessionMaxMinutes.");
			}

			if (IdleTouchIntervalMinutes < 0)
			{
				errors.Add("SecurityLifecycle.IdleTouchIntervalMinutes cannot be negative.");
			}
		}

		return errors;
	}
}

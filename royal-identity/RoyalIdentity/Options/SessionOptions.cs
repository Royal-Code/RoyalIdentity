namespace RoyalIdentity.Options;

/// <summary>
/// Realm-scoped SSO session lifetime and passive-invalidation policy. This is an <b>IdP concern</b> (the session is a
/// core concept — <see cref="Users.UserSession"/>), not the user-accounts module's: it gives behavior to
/// <see cref="Users.UserSession.ExpiresAt"/> (Realm-only — ADR-017 §2.12) and gates passive session invalidation by the
/// account security marker (<c>SessionsValidAfter</c>), which the IdP reads through the optional
/// <c>IUserSecurityStateProvider</c> capability (Q15). Token lifetimes, audit and the per-credential-trigger active
/// revocation policy stay in the module (Q7).
/// </summary>
public class SessionOptions
{
    /// <summary>
    /// Creates a new instance with defaults that preserve current behavior (expiration and state invalidation off).
    /// </summary>
    public SessionOptions()
    {
    }

    /// <summary>
    /// Creates an independent copy of another instance.
    /// </summary>
    public SessionOptions(SessionOptions other)
    {
        EnableSsoSessionExpiration = other.EnableSsoSessionExpiration;
        SsoSessionMaxMinutes = other.SsoSessionMaxMinutes;
        SsoSessionIdleMinutes = other.SsoSessionIdleMinutes;
        IdleTouchIntervalMinutes = other.IdleTouchIntervalMinutes;
        EnableSessionInvalidationByState = other.EnableSessionInvalidationByState;
    }

    /// <summary>
    /// Gets or sets whether the realm enforces an SSO session lifetime (gives behavior to
    /// <see cref="Users.UserSession.ExpiresAt"/>, Realm-only — ADR-017 §2.12). Default off preserves the reserved field.
    /// </summary>
    public bool EnableSsoSessionExpiration { get; set; } = false;

    /// <summary>
    /// Gets or sets the SSO session maximum lifetime, in minutes, used when
    /// <see cref="EnableSsoSessionExpiration"/> is on.
    /// </summary>
    public int SsoSessionMaxMinutes { get; set; } = 600;

    /// <summary>
    /// Gets or sets the SSO session idle timeout, in minutes, used when <see cref="EnableSsoSessionExpiration"/> is
    /// on. Zero disables the idle timeout (only the max lifetime applies).
    /// </summary>
    public int SsoSessionIdleMinutes { get; set; } = 0;

    /// <summary>
    /// Gets or sets the minimum window, in minutes, between idle touches of <c>UserSession.LastSeenAt</c>. Throttles
    /// writes: the store only updates when the last touch is older than this window.
    /// </summary>
    public int IdleTouchIntervalMinutes { get; set; } = 5;

    /// <summary>
    /// Gets or sets whether sessions are validated passively by <c>SessionsValidAfter</c> (a session started before
    /// the marker is invalid). Default off preserves current behavior. When on, the user provider must expose the
    /// security-state capability — see <see cref="RequiresSecurityStateProvider"/>.
    /// </summary>
    public bool EnableSessionInvalidationByState { get; set; } = false;

    /// <summary>
    /// Gets whether this realm requires a security-state provider capability (Q15). When true and the user provider
    /// does not expose <c>IUserSecurityStateProvider</c>, the composition is a configuration error.
    /// </summary>
    public bool RequiresSecurityStateProvider => EnableSessionInvalidationByState;

    /// <summary>
    /// Validates internal consistency of the session options.
    /// </summary>
    /// <returns>A list of configuration errors. Empty means valid.</returns>
    public IReadOnlyList<string> Validate()
    {
        List<string> errors = [];

        if (!EnableSsoSessionExpiration)
        {
            return errors;
        }

        if (SsoSessionMaxMinutes <= 0)
        {
            errors.Add("Session.SsoSessionMaxMinutes must be greater than zero when SSO session expiration is enabled.");
        }

        if (SsoSessionIdleMinutes < 0)
        {
            errors.Add("Session.SsoSessionIdleMinutes cannot be negative.");
        }

        if (SsoSessionIdleMinutes > 0 && SsoSessionMaxMinutes > 0 && SsoSessionIdleMinutes > SsoSessionMaxMinutes)
        {
            errors.Add("Session.SsoSessionIdleMinutes cannot be greater than SsoSessionMaxMinutes.");
        }

        if (IdleTouchIntervalMinutes < 0)
        {
            errors.Add("Session.IdleTouchIntervalMinutes cannot be negative.");
        }

        if (SsoSessionIdleMinutes > 0)
        {
            if (IdleTouchIntervalMinutes <= 0)
            {
                errors.Add("Session.IdleTouchIntervalMinutes must be greater than zero when SsoSessionIdleMinutes is enabled.");
            }

            if (IdleTouchIntervalMinutes >= SsoSessionIdleMinutes)
            {
                errors.Add("Session.IdleTouchIntervalMinutes must be less than SsoSessionIdleMinutes when idle timeout is enabled.");
            }
        }

        return errors;
    }
}

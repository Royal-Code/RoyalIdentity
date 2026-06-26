using RoyalIdentity.Options;

namespace Tests.Integration.Options;

/// <summary>
/// Fase 8 (plan-users-security-lifecycle.md) — the SSO session lifetime and passive-invalidation policy is an IdP
/// concern living in <see cref="SessionOptions"/> (moved out of the user module, ADR-017 §2.12). These cover the
/// defaults, the capability requirement and the validation that the module used to own.
/// </summary>
public class SessionOptionsTests
{
    [Fact]
    public void Defaults_PreserveCurrentBehavior()
    {
        var options = new SessionOptions();

        Assert.False(options.EnableSsoSessionExpiration);
        Assert.False(options.EnableSessionInvalidationByState);
        Assert.False(options.RequiresSecurityStateProvider);
        Assert.Equal(600, options.SsoSessionMaxMinutes);
        Assert.Equal(0, options.SsoSessionIdleMinutes);
        Assert.Equal(5, options.IdleTouchIntervalMinutes);
        Assert.Empty(options.Validate());
    }

    [Fact]
    public void RequiresSecurityStateProvider_FollowsStateInvalidation()
    {
        var options = new SessionOptions { EnableSessionInvalidationByState = true };

        Assert.True(options.RequiresSecurityStateProvider);
    }

    [Fact]
    public void CopyConstructor_CreatesIndependentCopy()
    {
        var source = new SessionOptions
        {
            EnableSsoSessionExpiration = true,
            SsoSessionMaxMinutes = 120,
            SsoSessionIdleMinutes = 20,
            IdleTouchIntervalMinutes = 3,
            EnableSessionInvalidationByState = true
        };

        var copy = new SessionOptions(source);

        source.SsoSessionMaxMinutes = 60;
        source.EnableSessionInvalidationByState = false;

        Assert.True(copy.EnableSsoSessionExpiration);
        Assert.Equal(120, copy.SsoSessionMaxMinutes);
        Assert.Equal(20, copy.SsoSessionIdleMinutes);
        Assert.Equal(3, copy.IdleTouchIntervalMinutes);
        Assert.True(copy.EnableSessionInvalidationByState);
    }

    [Fact]
    public void Validate_Rejects_SsoIdleGreaterThanMax_WhenExpirationEnabled()
    {
        var options = new SessionOptions
        {
            EnableSsoSessionExpiration = true,
            SsoSessionMaxMinutes = 60,
            SsoSessionIdleMinutes = 120
        };

        Assert.Contains(options.Validate(), e => e.Contains("SsoSessionIdleMinutes", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_Rejects_ZeroIdleTouchInterval_WhenIdleTimeoutEnabled()
    {
        var options = new SessionOptions
        {
            EnableSsoSessionExpiration = true,
            SsoSessionMaxMinutes = 60,
            SsoSessionIdleMinutes = 10,
            IdleTouchIntervalMinutes = 0
        };

        Assert.Contains(options.Validate(), e => e.Contains("IdleTouchIntervalMinutes", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_Rejects_IdleTouchIntervalAtOrAboveIdleTimeout()
    {
        var options = new SessionOptions
        {
            EnableSsoSessionExpiration = true,
            SsoSessionMaxMinutes = 60,
            SsoSessionIdleMinutes = 10,
            IdleTouchIntervalMinutes = 10
        };

        Assert.Contains(options.Validate(), e => e.Contains("less than SsoSessionIdleMinutes", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_Ignores_SessionSettings_WhenExpirationDisabled()
    {
        var options = new SessionOptions
        {
            EnableSsoSessionExpiration = false,
            SsoSessionMaxMinutes = -1,
            SsoSessionIdleMinutes = -1
        };

        Assert.Empty(options.Validate());
    }
}

using RoyalIdentity.Options;

namespace Tests.Identity.ReplayProtection;

/// <summary>
/// Covers <c>Authentication.ClientAssertionMaxLifetime</c> (plan-replay-protection DF19/DF21): the default the
/// clock skew forces, the accepted range, and the copy that keeps realms from sharing one instance.
/// </summary>
public class ClientAssertionMaxLifetimeOptionTests
{
    [Fact]
    public void Default_IsTenMinutes_AndIsValid()
    {
        var options = new AuthenticationOptions();

        Assert.Equal(TimeSpan.FromMinutes(10), options.ClientAssertionMaxLifetime);
        Assert.Empty(options.Validate());
    }

    // Ten minutes is not an arbitrary round number: it is the floor coherent with the five-minute skew the
    // assertion validation already tolerates. Lowering the default below the skew would make the server refuse
    // assertions its own tolerance says it accepts.
    [Fact]
    public void Default_CoversAnAssertionEmittedByAClockAheadByTheToleratedSkew()
    {
        var options = new AuthenticationOptions();
        var clientLifetime = TimeSpan.FromMinutes(5);
        var clientClockAhead = TimeSpan.FromMinutes(5);

        Assert.True(clientLifetime + clientClockAhead <= options.ClientAssertionMaxLifetime);
    }

    [Theory]
    [InlineData(1)]                 // exact minimum
    [InlineData(600)]               // default
    [InlineData(3600)]              // exact maximum
    public void ValuesInsideTheRange_AreAccepted(int seconds)
    {
        var options = new AuthenticationOptions
        {
            ClientAssertionMaxLifetime = TimeSpan.FromSeconds(seconds),
        };

        Assert.Empty(options.Validate());
    }

    [Theory]
    [InlineData(0)]                 // zero
    [InlineData(-1000)]             // negative
    [InlineData(999)]               // positive but below the one-second minimum
    [InlineData(3_600_001)]         // just above one hour
    public void ValuesOutsideTheRange_AreConfigurationErrors(int milliseconds)
    {
        var options = new AuthenticationOptions
        {
            ClientAssertionMaxLifetime = TimeSpan.FromMilliseconds(milliseconds),
        };

        Assert.Contains(
            options.Validate(),
            error => error.Contains("ClientAssertionMaxLifetime", StringComparison.Ordinal));
    }

    // Below the skew is deliberate hardening, not a configuration error: the realm accepts losing clients whose
    // clock runs ahead.
    [Fact]
    public void ValueBelowTheToleratedSkew_IsAllowedAsDeliberateHardening()
    {
        var options = new AuthenticationOptions
        {
            ClientAssertionMaxLifetime = TimeSpan.FromMinutes(1),
        };

        Assert.Empty(options.Validate());
    }

    [Fact]
    public void CopyConstructor_CarriesTheValue()
    {
        var source = new AuthenticationOptions
        {
            ClientAssertionMaxLifetime = TimeSpan.FromMinutes(42),
        };

        var copy = new AuthenticationOptions(source);

        Assert.Equal(TimeSpan.FromMinutes(42), copy.ClientAssertionMaxLifetime);
    }

    [Fact]
    public void RealmOptions_CopyOnCreate_DoesNotShareTheValueWithTheServerDefaults()
    {
        var serverOptions = new ServerOptions();
        serverOptions.Authentication.ClientAssertionMaxLifetime = TimeSpan.FromMinutes(30);

        var realmOptions = new RealmOptions(serverOptions);
        realmOptions.Authentication.ClientAssertionMaxLifetime = TimeSpan.FromMinutes(2);

        Assert.Equal(TimeSpan.FromMinutes(30), serverOptions.Authentication.ClientAssertionMaxLifetime);
        Assert.Equal(TimeSpan.FromMinutes(2), realmOptions.Authentication.ClientAssertionMaxLifetime);
    }
}

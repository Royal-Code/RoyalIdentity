using System.Security.Claims;
using Microsoft.Extensions.Options;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Users.Contracts;
using RoyalIdentity.Utils;

namespace RoyalIdentity.Users.Defaults;

public sealed class DefaultIdentityUser : IdentityUser
{
    private readonly UserDetails details;
    private readonly AccountOptions options;
    private readonly IUserSessionStore sessionStore;
    private readonly IUserDetailsStore userStore;
    private readonly IPasswordProtector passwordProtector;

    public DefaultIdentityUser(
        UserDetails details,
        IOptions<AccountOptions> options,
        IUserSessionStore sessionStore,
        IUserDetailsStore userStore,
        IPasswordProtector passwordProtector)
    {
        this.details = details;
        this.options = options.Value;
        this.sessionStore = sessionStore;
        this.userStore = userStore;
        this.passwordProtector = passwordProtector;
    }

    public override string UserName => details.Username;

    public override string DysplayName => details.DisplayName;

    public override bool IsActive => details.IsActive;

    public override async ValueTask<ValidateCredentialsResult> ValidateCredentialsAsync(string password, CancellationToken ct = default)
    {
        var isValid = await passwordProtector.VerifyPasswordAsync(password, details.PasswordHash, ct);
        if (!isValid)
        {
            details.LoginAttemptsWithPasswordErrors++;
            await userStore.SaveUserDetailsAsync(details, ct);
            return false;
        }

        if (details.LoginAttemptsWithPasswordErrors is not 0)
        {
            details.LoginAttemptsWithPasswordErrors = 0;
            await userStore.SaveUserDetailsAsync(details, ct);
        }

        // start a new session
        var session = await sessionStore.StartSessionAsync(details.Username, ct);

        return session;
    }

    public override ValueTask<bool> IsBlockedAsync(CancellationToken ct = default)
    {
        var isBlock = details.LoginAttemptsWithPasswordErrors >= options.UserBlockingAttempts;
        return new ValueTask<bool>(isBlock);
    }

    public override async ValueTask<ClaimsPrincipal> CreatePrincipalAsync(
        IdentitySession? session, string? amr, CancellationToken ct = default)
    {
        // check if session was created, if not, load it
        var currentSession = session ?? await sessionStore.GetCurrentSessionAsync(ct);

        if (currentSession is null)
            throw new InvalidOperationException("There is no active session for the user.");

        var claims = new List<Claim>
        {
            new(JwtClaimTypes.Subject, details.Username),
            new(JwtClaimTypes.Name, details.DisplayName),
            new(JwtClaimTypes.AuthenticationTime, new DateTimeOffset(currentSession.StartedAt).ToUnixTimeSeconds().ToString()),
            new(JwtClaimTypes.SessionId, currentSession.Id),
            new(JwtClaimTypes.IdentityProvider, ServerConstants.LocalIdentityProvider)
        };

        if (amr.IsPresent())
            claims.Add(new(JwtClaimTypes.AuthenticationMethod, amr));

        claims.AddRange(details.Claims);

        var identity = new ClaimsIdentity(
            claims.Distinct(new ClaimComparer()),
            Constants.ServerAuthenticationType,
            JwtClaimTypes.Subject,
            JwtClaimTypes.Role);

        return new ClaimsPrincipal(identity);
    }
}
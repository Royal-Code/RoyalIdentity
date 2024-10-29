using System.Security.Claims;
using Microsoft.Extensions.Options;
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

    private IdentitySession? session;

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

    public override string UserName => details.UserName;

    public override string DysplayName => details.DisplayName;
    public override bool IsActive => details.IsActive;

    public override async ValueTask<bool> ValidateCredentialsAsync(string password, CancellationToken ct = default)
    {
        var isValid = await passwordProtector.VerifyPasswordAsync(details.PasswordHash, password, ct);
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
        session = await sessionStore.StartSessionAsync(details.UserName, ct);

        return true;
    }

    public override ValueTask<bool> IsBlockedAsync(CancellationToken ct = default)
    {
        var isBlock = details.LoginAttemptsWithPasswordErrors >= options.UserBlockingAttempts;
        return new ValueTask<bool>(isBlock);
    }

    public override async ValueTask<ClaimsPrincipal> CreatePrincipalAsync(CancellationToken ct = default)
    {
        // check if session was created, if not, load it
        var currentSession = session ?? await sessionStore.GetCurrentSessionAsync(ct);

        if (currentSession is null)
            throw new InvalidOperationException("There is no active session for the user.");

        var claims = new List<Claim>();
        claims.Add(new Claim(JwtClaimTypes.Subject, details.UserName));
        claims.Add(new Claim(JwtClaimTypes.Name, details.DisplayName));
        claims.Add(new Claim(JwtClaimTypes.AuthenticationTime, new DateTimeOffset( currentSession.StartedAt).ToUnixTimeSeconds().ToString()));

        claims.AddRange(details.Claims);

        var identity = new ClaimsIdentity(
            claims.Distinct(new ClaimComparer()),
            Constants.ServerAuthenticationType,
            JwtClaimTypes.Subject,
            JwtClaimTypes.Role);

        return new ClaimsPrincipal(identity);
    }
}
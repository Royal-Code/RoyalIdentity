using System.Security.Claims;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Users.Contracts;

namespace RoyalIdentity.Users.Defaults;

public sealed class DefaultIdentityUser : IdentityUser
{
    private readonly UserDetails details;
    private readonly AccountOptions options;
    private readonly IUserSessionStore sessionStore;
    private readonly IUserDetailsStore userStore;
    private readonly IPasswordProtector passwordProtector;
    private readonly TimeProvider clock;

    public DefaultIdentityUser(
        UserDetails details,
        AccountOptions options,
        IUserSessionStore sessionStore,
        IUserDetailsStore userStore,
        IPasswordProtector passwordProtector,
        TimeProvider clock)
    {
        this.details = details;
        this.options = options;
        this.sessionStore = sessionStore;
        this.userStore = userStore;
        this.passwordProtector = passwordProtector;
        this.clock = clock;
    }

    public override string UserName => details.Username;

    public override string DysplayName => details.DisplayName;

    public override bool IsActive => details.IsActive;

    public override async ValueTask<ValidateCredentialsResult> AuthenticateAndStartSessionAsync(string password, CancellationToken ct = default)
    {
        // when does not have a password hash, password authentication is not allowed, then return false
        if (details.PasswordHash is null)
            return false;

        var isValid = await passwordProtector.VerifyPasswordAsync(password, details.PasswordHash, ct);
        if (!isValid)
        {
            details.LoginAttemptsWithPasswordErrors++;
            details.LastPasswordError = clock.GetUtcNow();
            await userStore.SaveUserDetailsAsync(details, ct);
            return false;
        }

        if (details.LoginAttemptsWithPasswordErrors is not 0)
        {
            details.LoginAttemptsWithPasswordErrors = 0;
            details.LastPasswordError = null;
            await userStore.SaveUserDetailsAsync(details, ct);
        }

        // start a new session
        var session = await sessionStore.StartSessionAsync(this, AuthenticationMethods.Password, ct);

        return session;
    }

    public override ValueTask<bool> IsLockoutAsync(CancellationToken ct = default)
    {
        if (options.PasswordOptions.MaxFailedAccessAttempts is 0)
            return new(false);

        var isLockout = details.LoginAttemptsWithPasswordErrors >= options.PasswordOptions.MaxFailedAccessAttempts;

        if (isLockout && 
            options.PasswordOptions.AccountLockoutDurationMinutes is not 0 && 
            details.LastPasswordError is not null)
        {
            var now = clock.GetUtcNow();
            var lockoutDuration = now.Subtract(details.LastPasswordError.Value).TotalMinutes;

            isLockout = lockoutDuration <= options.PasswordOptions.AccountLockoutDurationMinutes;
        }

        return new(isLockout);
    }

    public override async ValueTask<ClaimsPrincipal> CreatePrincipalAsync(
        IdentitySession? session, CancellationToken ct = default)
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
            new(JwtClaimTypes.IdentityProvider, ServerConstants.LocalIdentityProvider),
            new(JwtClaimTypes.AuthenticationMethod, currentSession.Amr)
        };

        foreach (var role in details.Roles)
            claims.Add(new(JwtClaimTypes.Role, role));

        claims.AddRange(details.Claims);

        var identity = claims.CreateIdentity();

        return new ClaimsPrincipal(identity);
    }
}
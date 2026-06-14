using System.Security.Claims;
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

    public override string DisplayName => details.DisplayName;

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
        // NOTE (plan Fase 5): the session is still created here as a side-effect of credential verification;
        // Fase 7 moves session creation to LoginFlowService at sign-in time.
        var session = new UserSession
        {
            Id = CryptoRandom.CreateUniqueId(16),
            SubjectId = details.SubjectId,
            AuthenticationMethod = Oidc.AuthMethods.Password,
            IdentityProvider = Server.LocalIdentityProvider,
            StartedAt = clock.GetUtcNow().UtcDateTime,
        };
        await sessionStore.CreateAsync(session, ct);

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

    public override ValueTask<ClaimsPrincipal> CreatePrincipalAsync(
        UserSession? session, CancellationToken ct = default)
    {
        // the session is always supplied by the sign-in flow (the pure store has no notion of "current").
        var currentSession = session
            ?? throw new InvalidOperationException("There is no active session for the user.");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, details.SubjectId),
            new(JwtRegisteredClaimNames.Name, details.DisplayName),
            new(JwtRegisteredClaimNames.AuthTime, new DateTimeOffset(currentSession.StartedAt).ToUnixTimeSeconds().ToString()),
            new(JwtRegisteredClaimNames.Sid, currentSession.Id),
            new(Jwt.ClaimTypes.IdentityProvider, currentSession.IdentityProvider),
            new(JwtRegisteredClaimNames.Amr, currentSession.AuthenticationMethod)
        };

        foreach (var role in details.Roles)
            claims.Add(new(Jwt.ClaimTypes.Role, role));

        claims.AddRange(details.Claims);

        var identity = claims.CreateIdentity();

        return new ValueTask<ClaimsPrincipal>(new ClaimsPrincipal(identity));
    }
}
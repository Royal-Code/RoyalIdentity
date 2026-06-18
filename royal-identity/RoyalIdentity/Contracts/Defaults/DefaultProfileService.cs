using Microsoft.Extensions.Logging;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using RoyalIdentity.Users.Contracts;
using System.Security.Claims;

namespace RoyalIdentity.Contracts.Defaults;

public class DefaultProfileService : IProfileService
{
    private readonly IUserDirectory userDirectory;
    private readonly IUserSessionService userSessionService;
    private readonly ILogger logger;

    public DefaultProfileService(
        IUserDirectory userDirectory,
        IUserSessionService userSessionService,
        ILogger<DefaultProfileService> logger)
    {
        this.userDirectory = userDirectory;
        this.userSessionService = userSessionService;
        this.logger = logger;
    }

    /// <summary>
    /// Sources the issued claims from the <see cref="IUserClaimsProvider"/> (claims seam, ADR-014 §2.9/§4):
    /// only primitives cross the boundary — the requested identity scope names and claim types go in, and
    /// <see cref="Claim"/>s come back ready to issue (no intermediate DTO; ADR-015 §2.4). The provider also
    /// enforces account-active (inactive ⇒ no claims). Roles are emitted by the provider as profile claims,
    /// never via the minimal session principal (ADR-014 §2.8).
    /// </summary>
    public async ValueTask GetProfileDataAsync(ProfileDataRequest request, CancellationToken ct)
    {
        var subjectId = request.Subject.GetSubjectId();

        var provider = userDirectory.GetClaimsProvider(request.Client.Realm);
        var identityScopeNames = request.RequestedResources.IdentityScopes.Select(s => s.Name).ToList();

        var userClaims = await provider.GetClaimsAsync(
            subjectId, identityScopeNames, request.RequestedClaimTypes, ct);

        foreach (var claim in userClaims)
            request.IssuedClaims.Add(claim);
    }

    /// <summary>
    /// Unified "active" rule (ADR-014 §2.7): the account must be active AND, when the principal is
    /// session-bound (has a <c>sid</c>), its session must be valid. A <b>missing</b> session for a principal
    /// that carries a <c>sid</c> is treated as <b>invalid</b> (not "no session"), so the cookie, token and
    /// authorize paths all agree. A principal without a <c>sid</c> is not session-bound, so only the account
    /// activity is checked.
    /// </summary>
    public async ValueTask<bool> IsActiveAsync(ClaimsPrincipal subject, Client client, string caller, CancellationToken ct)
    {
        var subjectId = subject.GetSubjectId();

        logger.LogDebug("Start User is active: {Subject}, Caller: {Caller}, Client: {Client}",
            subjectId, caller, client.Id);

        // account active — via the borda facade (realm bound by the gateway).
        var subjectStore = userDirectory.GetSubjectStore(client.Realm);
        if (!await subjectStore.IsActiveAsync(subjectId, ct))
            return false;

        // session valid — only when the principal is session-bound; absent session ⇒ invalid.
        if (HasSessionId(subject) && !await userSessionService.IsSessionValidAsync(subject, ct))
            return false;

        return true;
    }

    private static bool HasSessionId(ClaimsPrincipal subject)
        => (subject.Identity as ClaimsIdentity)?.FindFirst(JwtRegisteredClaimNames.Sid) is not null;
}

using Microsoft.Extensions.Logging;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using RoyalIdentity.Options;
using RoyalIdentity.Users.Contracts;
using System.Security.Claims;

namespace RoyalIdentity.Contracts.Defaults;

public class DefaultProfileService : IProfileService
{
    private readonly IStorage storage;
    private readonly IUserDirectory userDirectory;
    private readonly IUserSessionService userSessionService;
    private readonly ILogger logger;

    public DefaultProfileService(
        IStorage storage,
        IUserDirectory userDirectory,
        IUserSessionService userSessionService,
        ILogger<DefaultProfileService> logger)
    {
        this.storage = storage;
        this.userDirectory = userDirectory;
        this.userSessionService = userSessionService;
        this.logger = logger;
    }

    public async ValueTask GetProfileDataAsync(ProfileDataRequest request, CancellationToken ct)
    {
        var userDetailsStore = storage.GetUserDetailsStore(request.Client.Realm);
        var userDetails = await userDetailsStore.GetUserDetailsAsync(request.Subject.GetSubjectId(), ct);
        if (userDetails is null || !userDetails.IsActive)
            return;

        // get all users claims
        IEnumerable<Claim> userClaims = userDetails.Claims;
        userClaims = userClaims.Concat(
            [
                new Claim(JwtRegisteredClaimNames.Name, userDetails.DisplayName),
                new Claim(Jwt.ClaimTypes.PreferredUserName, userDetails.DisplayName)
            ]);

        // filter the requested claims
        var requestedClaim = request.RequestedClaimTypes;
        request.IssuedClaims.AddRange(userClaims.Where(c => requestedClaim.Contains(c.Type)));

        // add the user's roles to the claims
        request.IssuedClaims.AddRange(userDetails.Roles.Select(r => new Claim(Jwt.ClaimTypes.Role, r)));
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

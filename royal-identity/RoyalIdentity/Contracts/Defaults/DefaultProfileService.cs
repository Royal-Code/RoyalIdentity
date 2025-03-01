using Microsoft.Extensions.Logging;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using RoyalIdentity.Options;
using System.Security.Claims;

namespace RoyalIdentity.Contracts.Defaults;

public class DefaultProfileService : IProfileService
{
    private readonly IStorage storage;
    private readonly ILogger logger;

    public DefaultProfileService(
        IStorage storage,
        ILogger<DefaultProfileService> logger)
    {
        this.storage = storage;
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
                new Claim(JwtClaimTypes.Subject, userDetails.Username),
                new Claim(JwtClaimTypes.Name, userDetails.DisplayName),
                new Claim(JwtClaimTypes.PreferredUserName, userDetails.DisplayName)
            ]);

        // filter the requested claims
        var requestedClaim = request.RequestedClaimTypes;
        request.IssuedClaims.AddRange(userClaims.Where(c => requestedClaim.Contains(c.Type)));

        // add the user's roles to the claims
        request.IssuedClaims.AddRange(userDetails.Roles.Select(r => new Claim(JwtClaimTypes.Role, r)));
    }

    public async ValueTask<bool> IsActiveAsync(ClaimsPrincipal subject, Client client, string caller, CancellationToken ct)
    {
        var subjectId = subject.GetSubjectId();
        var sessionId = subject.GetSessionId();

        logger.LogDebug("Start User is active: {Subject} - {Session}, Caller: {Caller}, Client: {Client}",
            subjectId, sessionId, caller, client.Id);

        var userDetailsStore = storage.GetUserDetailsStore(client.Realm);
        var userDetails = await userDetailsStore.GetUserDetailsAsync(subjectId, ct);
        if (userDetails is null || !userDetails.IsActive)
            return false;

        var userSessionStore = storage.GetUserSessionStore(client.Realm);
        var userSession = await userSessionStore.GetUserSessionAsync(sessionId, ct);
        if (userSession is not null && !userSession.IsActive)
            return false;

        return true;
    }
}

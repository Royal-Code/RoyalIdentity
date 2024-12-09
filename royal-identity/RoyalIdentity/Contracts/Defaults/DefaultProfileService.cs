using Microsoft.Extensions.Logging;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using RoyalIdentity.Users.Contracts;
using System.Security.Claims;

namespace RoyalIdentity.Contracts.Defaults;

public class DefaultProfileService : IProfileService
{
    private readonly IUserDetailsStore userDetailsStore;
    private readonly IUserSessionStore userSessionStore;
    private readonly ILogger logger;

    public DefaultProfileService(
        IUserDetailsStore userDetailsStore,
        IUserSessionStore userSessionStore,
        ILogger<DefaultProfileService> logger)
    {
        this.userDetailsStore = userDetailsStore;
        this.userSessionStore = userSessionStore;
        this.logger = logger;
    }

    public ValueTask GetProfileDataAsync(ProfileDataRequest request, CancellationToken ct)
    {
        request.IssuedClaims.AddRange(request.Subject.Claims);
        return ValueTask.CompletedTask;
    }

    public async ValueTask<bool> IsActiveAsync(ClaimsPrincipal subject, Client client, string caller, CancellationToken ct)
    {
        var subjectId = subject.GetSubjectId();
        var sessionId = subject.GetSessionId();

        logger.LogDebug("Start User is active: {Subject} - {Session}, Caller: {Caller}, Client: {Client}",
            subjectId, sessionId, caller, client.Id);

        var userDetails = await userDetailsStore.GetUserDetailsAsync(subjectId, ct);
        if (userDetails is null || !userDetails.IsActive)
            return false;

        var userSession = await userSessionStore.GetUserSessionAsync(sessionId, ct);
        if (userSession is not null && !userSession.IsActive)
            return false;

        return true;
    }
}

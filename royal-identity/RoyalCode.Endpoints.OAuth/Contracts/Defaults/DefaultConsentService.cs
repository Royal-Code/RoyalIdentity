using Microsoft.Extensions.Logging;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using RoyalIdentity.Options;
using System.Security.Claims;

namespace RoyalIdentity.Contracts.Defaults;

public class DefaultConsentService : IConsentService
{
    private readonly ILogger logger;
    private readonly IUserConsentStore userConsentStore;
    private readonly TimeProvider clock;

    public DefaultConsentService(
        IUserConsentStore userConsentStore, 
        TimeProvider clock,
        ILogger<DefaultConsentService> logger)
    {
        this.logger = logger;
        this.userConsentStore = userConsentStore;
        this.clock = clock;
    }

    public async ValueTask<bool> RequiresConsentAsync(ClaimsPrincipal subject, Client client, Resources resources)
    {
        if (!client.RequireConsent)
        {
            logger.LogDebug("Client is configured to not require consent, no consent is required");
            return false;
        }

        if (resources.None())
        {
            logger.LogDebug("No scopes being requested, no consent is required");
            return false;
        }

        if (!client.AllowRememberConsent)
        {
            logger.LogDebug("Client is configured to not allow remembering consent, consent is required");
            return true;
        }

        var consent = await userConsentStore.GetUserConsentAsync(subject.GetSubjectId(), client.Id);

        if (consent is null)
        {
            logger.LogDebug("Found no prior consent from consent store, consent is required");
            return true;
        }

        if (consent.Expiration.HasExpired(clock.GetUtcNow().UtcDateTime))
        {
            logger.LogDebug("Consent found in consent store is expired, consent is required");
            await userConsentStore.RemoveUserConsentAsync(consent.SubjectId, consent.ClientId);
            return true;
        }

        if (consent.Scopes != null)
        {
            var intersectCount = resources.RequestedScopes.Intersect(consent.Scopes).Count();
            var different = resources.RequestedScopes.Count != intersectCount;

            if (different)
            {
                logger.LogDebug("Consent found in consent store is different than current request, consent is required");
            }
            else
            {
                logger.LogDebug("Consent found in consent store is same as current request, consent is not required");
            }

            return different;
        }

        logger.LogDebug("Consent found in consent store has no scopes, consent is required");

        return true;
    }

    public async Task UpdateConsentAsync(ClaimsPrincipal subject, Client client, IEnumerable<string> scopes)
    {
        if (client.AllowRememberConsent)
        {
            var subjectId = subject.GetSubjectId();
            var clientId = client.Id;

            if (scopes.Any())
            {
                logger.LogDebug(
                    "Client allows remembering consent, and consent given. Updating consent store for subject: {Subject}",
                    subject.GetSubjectId());

                var consent = new Consent
                {
                    CreationTime = clock.GetUtcNow().UtcDateTime,
                    SubjectId = subjectId,
                    ClientId = clientId,
                    Scopes = scopes.ToHashSet()
                };

                if (client.ConsentLifetime.HasValue)
                {
                    consent.Expiration = consent.CreationTime.AddSeconds(client.ConsentLifetime.Value);
                }

                await userConsentStore.StoreUserConsentAsync(consent);
            }
            else
            {
                logger.LogDebug(
                    "Client allows remembering consent, and no scopes provided. Removing consent from consent store for subject: {Subject}", 
                    subject.GetSubjectId());

                await userConsentStore.RemoveUserConsentAsync(subjectId, clientId);
            }
        }
    }
}

using Microsoft.Extensions.Logging;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using System.Diagnostics.CodeAnalysis;
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

    public async ValueTask<bool> RequiresConsentAsync(ClaimsPrincipal subject, Client client, Resources resources, CancellationToken ct)
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

        var consent = await userConsentStore.GetUserConsentAsync(subject.GetSubjectId(), client.Id, ct);

        bool consented = await Consented(consent, resources, ct);

        // when consented, but has temporary consent, remove it and update the store
        if (consented && consent!.RemoveTemporaryConsents())
        {
            await userConsentStore.StoreUserConsentAsync(consent, ct);
        }

        return !consented;
    }

    public async Task UpdateConsentAsync(ClaimsPrincipal subject, Client client, IEnumerable<ConsentedScope> scopes, CancellationToken ct)
    {
        var subjectId = subject.GetSubjectId();
        var clientId = client.Id;

        if (scopes.None())
        {
            logger.LogDebug(
                "No scopes provided. Removing consent from consent store for subject: {Subject}, {Client}",
                subjectId,
                clientId);

            await userConsentStore.RemoveUserConsentAsync(subjectId, clientId, ct);
        }

        var now = clock.GetUtcNow().UtcDateTime;

        // tries to get an existing consent, if it doesn't exist then it creates
        var consent = await userConsentStore.GetUserConsentAsync(subjectId, clientId, ct);
        consent ??= new Consent
        {
            CreationTime = now,
            SubjectId = subjectId,
            ClientId = clientId,
        };

        consent.AddScopes(scopes);

        if (client.ConsentLifetime.HasValue)
        {
            consent.Expiration = now.AddSeconds(client.ConsentLifetime.Value);
        }

        await userConsentStore.StoreUserConsentAsync(consent, ct);
    }

    public async ValueTask<bool> ValidateConsentAsync(ClaimsPrincipal subject, Client client, Resources resources, CancellationToken ct)
    {
        if (!client.RequireConsent)
        {
            logger.LogDebug("Client do not require consent, consent validation success");
            return true;
        }

        if (resources.None())
        {
            logger.LogDebug("No scopes being requested, consent validation success");
            return true;
        }

        if (!subject.IsAuthenticated())
        {
            return false;
        }

        var consent = await userConsentStore.GetUserConsentAsync(subject.GetSubjectId(), client.Id, ct);

        return await Consented(consent, resources, ct);
    }

    private async Task<bool> Consented([NotNullWhen(true)]Consent? consent, Resources resources, CancellationToken ct)
    {
        if (consent is null)
        {
            logger.LogDebug("Consent not found from consent store, consent validation failure");
            return false;
        }

        if (consent.Expiration.HasExpired(clock.GetUtcNow().UtcDateTime))
        {
            logger.LogDebug("Consent found in consent store is expired, consent validation failure");
            await userConsentStore.RemoveUserConsentAsync(consent.SubjectId, consent.ClientId, ct);
            return false;
        }

        bool consented;
        if (consent.Scopes is not null)
        {
            consented = resources.IntersectConsentScopes(consent.GetValidScopes());

            if (consented)
                logger.LogDebug("Consent found in consent store is same as current request, consent validation success");
            else
                logger.LogDebug("Consent found in consent store is different than current request, consent validation failure");
        }
        else
        {
            logger.LogDebug("Consent found in consent store has no scopes, consent validation failure");
            consented = false;
        }

        return consented;
    }
}

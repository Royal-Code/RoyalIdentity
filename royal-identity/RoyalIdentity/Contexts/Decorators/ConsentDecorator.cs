using Microsoft.Extensions.Logging;
using RoyalIdentity.Contracts;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Responses;

namespace RoyalIdentity.Contexts.Decorators;

public class ConsentDecorator : IDecorator<AuthorizeContext>
{
    private readonly IConsentService consent;
    private readonly ILogger logger;

    public ConsentDecorator(IConsentService consent, ILogger<ConsentDecorator> logger)
    {
        this.consent = consent;
        this.logger = logger;
    }

    public async Task Decorate(AuthorizeContext context, Func<Task> next, CancellationToken ct)
    {
        logger.LogDebug("Start authorize consent validation");

        context.ClientParameters.AssertHasClient();

        if (context.PromptModes.Count is not 0 &&
            !context.PromptModes.Contains(OidcConstants.PromptModes.None) &&
            !context.PromptModes.Contains(OidcConstants.PromptModes.Consent))
        {
            logger.LogError(context, "Invalid prompt mode", context.PromptModes.ToSpaceSeparatedString());

            context.InvalidRequest("Invalid prompt mode", context.PromptModes.ToSpaceSeparatedString());

            return;
        }

        var consentRequired = await consent.RequiresConsentAsync(
            context.Subject, 
            context.ClientParameters.Client,
            context.Resources,
            ct);

        if (consentRequired && context.PromptModes.Contains(OidcConstants.PromptModes.None))
        {
            logger.LogError(context, "Error: prompt=none requested, but consent is required.", context.PromptModes.ToSpaceSeparatedString());

            context.InvalidRequest("Invalid prompt mode", "consent is required");

            return;
        }

        if (context.PromptModes.Contains(OidcConstants.PromptModes.Consent) || consentRequired)
        {
            logger.LogInformation("Showing consent: User has not yet consented");

            // user was not yet shown consent screen
            context.Response = new InteractionResponse(context)
            {
                IsConsent = true
            };

            return;
        }

        await next();
    }
}

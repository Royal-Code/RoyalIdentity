using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoyalIdentity.Contracts;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Responses;

namespace RoyalIdentity.Contexts.Decorators;

public class ConsentDecorator : IDecorator<AuthorizeContext>
{
    private readonly ServerOptions options;
    private readonly IConsentService consent;
    private readonly ILogger logger;

    public ConsentDecorator(IOptions<ServerOptions> options, IConsentService consent, ILogger<ConsentDecorator> logger)
    {
        this.options = options.Value;
        this.consent = consent;
        this.logger = logger;
    }

    public async Task Decorate(AuthorizeContext context, Func<Task> next, CancellationToken ct)
    {
        context.AssertHasClient();

        if (context.PromptModes.Count is not 0 &&
            !context.PromptModes.Contains(OidcConstants.PromptModes.None) &&
            !context.PromptModes.Contains(OidcConstants.PromptModes.Consent))
        {
            logger.LogError(options, "Invalid prompt mode", context.PromptModes.ToSpaceSeparatedString(), context);

            context.InvalidRequest("Invalid prompt mode", context.PromptModes.ToSpaceSeparatedString());

            return;
        }

        var consentRequired = await consent.RequiresConsentAsync(context.Subject, context.Client, context.Resources, ct);

        if (consentRequired && context.PromptModes.Contains(OidcConstants.PromptModes.None))
        {
            logger.LogError(options, "Error: prompt=none requested, but consent is required.", context.PromptModes.ToSpaceSeparatedString(), context);

            context.InvalidRequest("Invalid prompt mode", "consent is required");

            return;
        }

        if (context.PromptModes.Contains(OidcConstants.PromptModes.Consent) || consentRequired)
        {
            logger.LogInformation("Showing consent: User has not yet consented");

            // user was not yet shown conset screen
            context.Response = new InteractionResponse(context)
            {
                IsConsent = true
            };

            return;
        }

        await next();
    }
}

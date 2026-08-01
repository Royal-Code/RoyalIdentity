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
    private readonly ISessionStateGenerator sessionStateGenerator;
    private readonly ILogger logger;

    public ConsentDecorator(
        IConsentService consent,
        ISessionStateGenerator sessionStateGenerator,
        ILogger<ConsentDecorator> logger)
    {
        this.consent = consent;
        this.sessionStateGenerator = sessionStateGenerator;
        this.logger = logger;
    }

    public async Task Decorate(AuthorizeContext context, Func<Task> next, CancellationToken ct)
    {
        logger.LogDebug("Start authorize consent validation");

        context.ClientParameters.AssertHasClient();

        if (context.UserDeniedConsent)
        {
            logger.LogInformation("Resource owner denied consent; returning access_denied to the client");

            context.Response = AuthorizeResponseFactory.CreateError(
                sessionStateGenerator,
                context,
                Oidc.Authorize.Errors.AccessDenied,
                "The resource owner denied the request.");

            return;
        }

        if (context.PromptModes.Count is not 0 &&
            !context.PromptModes.Contains(Oidc.PromptModes.None) &&
            !context.PromptModes.Contains(Oidc.PromptModes.Consent))
        {
            logger.LogError(context, "Invalid prompt mode", context.PromptModes.ToSpaceSeparatedString());

            context.Error(
                Oidc.Authorize.Errors.InvalidRequest,
                $"Invalid prompt mode: {context.PromptModes.ToSpaceSeparatedString()}");

            return;
        }

        var consentRequired = await consent.RequiresConsentAsync(
            context.Subject, 
            context.ClientParameters.Client,
            context.Scopes,
            ct);

        if (consentRequired && context.PromptModes.Contains(Oidc.PromptModes.None))
        {
            logger.LogError(context, "Error: prompt=none requested, but consent is required.", context.PromptModes.ToSpaceSeparatedString());

            context.Response = AuthorizeResponseFactory.Interaction(
                sessionStateGenerator,
                context,
                AuthorizeInteractionKind.Consent,
                "The request requires consent.");

            return;
        }

        if (context.PromptModes.Contains(Oidc.PromptModes.Consent) || consentRequired)
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

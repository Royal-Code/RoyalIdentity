using Microsoft.Extensions.Logging;
using RoyalIdentity.Contracts;
using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Contexts.Validators;

public class ConsentValidator : IValidator<AuthorizeValidateContext>
{
    private readonly IConsentService consent;
    private readonly ILogger logger;

    public ConsentValidator(IConsentService consent, ILogger<ConsentValidator> logger)
    {
        this.consent = consent;
        this.logger = logger;
    }

    public async ValueTask Validate(AuthorizeValidateContext context, CancellationToken ct)
    {
        context.AssertHasClient();

        logger.LogDebug("Start consent validation");

        var concented = await consent.ValidateConsentAsync(context.Subject, context.Client, context.Resources, ct);

        logger.LogDebug("Consent validation result: {Concented}", concented ? "Concented" : "concent required");

        context.RequiredConsent = !concented;
    }
}

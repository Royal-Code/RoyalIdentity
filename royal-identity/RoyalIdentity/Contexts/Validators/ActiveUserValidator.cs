using Microsoft.Extensions.Logging;
using RoyalIdentity.Contracts;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Abstractions;

namespace RoyalIdentity.Contexts.Validators;

public class ActiveUserValidator : IValidator<ITokenEndpointContextBase>
{
    private readonly IProfileService profileService;
    private readonly ILogger logger;

    public ActiveUserValidator(IProfileService profileService, ILogger<ActiveUserValidator> logger)
    {
        this.profileService = profileService;
        this.logger = logger;
    }

    public async ValueTask Validate(ITokenEndpointContextBase context, CancellationToken ct)
    {
        context.AssertHasClient();

        var subject = context.GetSubject();

        if (subject is null)
        {
            logger.LogError(context, "No subject found in the context.");
            throw new InvalidOperationException("No subject found in the context.");
        }

        var isActive = await profileService.IsActiveAsync(subject, context.Client, context.GrantType, ct);

        if (!isActive)
        {
            logger.LogError(context, "User is not active.");
            context.Error(OidcConstants.TokenErrors.InvalidGrant, "User is not active.");
        }
    }
}
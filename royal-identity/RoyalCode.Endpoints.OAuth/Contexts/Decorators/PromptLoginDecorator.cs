using Microsoft.Extensions.Logging;
using RoyalIdentity.Contracts;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Responses;

namespace RoyalIdentity.Contexts.Decorators;

public class PromptLoginDecorator : IDecorator<AuthorizeContext>
{
    private readonly IProfileService profileService;
    private readonly ILogger logger;
    private readonly TimeProvider time;

    public PromptLoginDecorator(
        IProfileService profileService, 
        ILogger<PromptLoginDecorator> logger,
        TimeProvider? time = null)
    {
        this.profileService = profileService;
        this.logger = logger;
        this.time = time ?? TimeProvider.System;
    }

    public async ValueTask Decorate(AuthorizeContext context, Func<ValueTask> next, CancellationToken ct)
    {
        context.AssertHasClient();

        if (context.PromptModes.Contains(OidcConstants.PromptModes.Login) ||
            context.PromptModes.Contains(OidcConstants.PromptModes.SelectAccount))
        {
            logger.LogInformation("Showing login: request contains prompt={0}", context.PromptModes.ToSpaceSeparatedString());

            // remove prompt so when we redirect back in from login page
            // we won't think we need to force a prompt again
            context.Raw.Remove(OidcConstants.AuthorizeRequest.Prompt);

            context.Response = new InteractionResponse(context)
            {
                IsLogin = true 
            };

            return;
        }

        var isUserActive = context.Identity?.IsAuthenticated is true &&
            await profileService.IsActiveAsync(
                context.Subject,
                context.Client,
                ServerConstants.ProfileIsActiveCallers.AuthorizeEndpoint);

        if (!isUserActive)
        {
            logger.LogInformation("Showing login: User is not authenticated or not active");

            context.Response = new InteractionResponse(context)
            {
                IsLogin = true
            };

            return;
        }

        // check current idp
        var currentIdp = context.Subject.GetIdentityProvider();

        // check if idp login hint matches current provider
        var idp = context.GetIdP();
        if (idp.IsPresent() && idp != currentIdp)
        {
            logger.LogInformation("Showing login: Current IdP ({CurrentIdp}) is not the requested IdP ({IdP})", currentIdp, idp);

            context.Response = new InteractionResponse(context)
            {
                IsLogin = true
            };

            return;
        }

        // check authentication freshness
        if (context.MaxAge.HasValue)
        {
            var authTime = context.Subject.GetAuthenticationTime();
            if (time.GetUtcNow() > authTime.AddSeconds(context.MaxAge.Value))
            {
                logger.LogInformation("Showing login: Requested MaxAge exceeded.");

                context.Response = new InteractionResponse(context)
                {
                    IsLogin = true
                };

                return;
            }
        }

        // check local idp restrictions
        if (currentIdp == ServerConstants.LocalIdentityProvider)
        {
            if (!context.Client.EnableLocalLogin)
            {
                logger.LogInformation("Showing login: User logged in locally, but client does not allow local logins");

                context.Response = new InteractionResponse(context)
                {
                    IsLogin = true
                };

                return;
            }
        }
        // check external idp restrictions if user not using local idp
        else if (context.Client.IdentityProviderRestrictions != null &&
            context.Client.IdentityProviderRestrictions.Any() &&
            !context.Client.IdentityProviderRestrictions.Contains(currentIdp))
        {
            logger.LogInformation("Showing login: User is logged in with idp: {IdP}, but idp not in client restriction list.", currentIdp);
            context.Response = new InteractionResponse(context)
            {
                IsLogin = true
            };

            return;
        }

        // check client's user SSO timeout
        if (context.Client.UserSsoLifetime.HasValue)
        {
            var authTimeEpoch = context.Subject.GetAuthenticationTimeEpoch();
            var nowEpoch = time.GetUtcNow().ToUnixTimeSeconds();

            var diff = nowEpoch - authTimeEpoch;
            if (diff > context.Client.UserSsoLifetime.Value)
            {
                logger.LogInformation("Showing login: User's auth session duration: {SessionDuration} exceeds client's user SSO lifetime: {UserSsoLifetime}.", diff, context.Client.UserSsoLifetime);

                logger.LogInformation("Showing login: User is logged in with idp: {IdP}, but idp not in client restriction list.", currentIdp);
                context.Response = new InteractionResponse(context)
                {
                    IsLogin = true
                };

                return;
            }
        }

        await next();
    }
}

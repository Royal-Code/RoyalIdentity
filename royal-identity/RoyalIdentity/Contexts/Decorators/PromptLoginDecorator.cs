using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts.Withs;
using RoyalIdentity.Contracts;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Responses;
using System.Security.Claims;

namespace RoyalIdentity.Contexts.Decorators;

public class PromptLoginDecorator : IDecorator<IWithPrompt>
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

    public async Task Decorate(IWithPrompt context, Func<Task> next, CancellationToken ct)
    {
        context.ClientParameters.AssertHasClient();

        if (context.PromptModes.Contains(OidcConstants.PromptModes.Login) ||
            context.PromptModes.Contains(OidcConstants.PromptModes.SelectAccount))
        {
            logger.LogInformation(
                "Showing login: request contains prompt={PromptModes}", 
                context.PromptModes.ToSpaceSeparatedString());

            // remove prompt so when we redirect back in from login page
            // we won't think we need to force a prompt again
            context.Raw.Remove(OidcConstants.AuthorizeRequest.Prompt);

            context.Response = new InteractionResponse(context)
            {
                IsLogin = true 
            };

            return;
        }

        var principal = context.Subject ?? new ClaimsPrincipal(new ClaimsIdentity());
        var identity = principal.Identity ?? new ClaimsIdentity();

        var isUserActive = identity.IsAuthenticated &&
            await profileService.IsActiveAsync(
                principal,
                context.ClientParameters.Client,
                ServerConstants.ProfileIsActiveCallers.AuthorizeEndpoint,
                ct);

        if (!isUserActive)
        {
            logger.LogInformation("Showing login: User is not authenticated or not active");

            context.Response = new InteractionResponse(context)
            {
                IsLogin = true
            };

            return;
        }

        // check current IdP
        var currentIdp = principal.GetIdentityProvider();

        // check authentication freshness
        if (context.MaxAge.HasValue)
        {
            var authTime = principal.GetAuthenticationTime();
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

        // check local IdP restrictions
        if (currentIdp == ServerConstants.LocalIdentityProvider)
        {
            if (!context.ClientParameters.Client.EnableLocalLogin)
            {
                logger.LogInformation("Showing login: User logged in locally, but client does not allow local logins");

                context.Response = new InteractionResponse(context)
                {
                    IsLogin = true
                };

                return;
            }
        }
        // check external IdP restrictions if user not using local IdP
        else if (context.ClientParameters.Client.IdentityProviderRestrictions.Count is not 0 &&
            !context.ClientParameters.Client.IdentityProviderRestrictions.Contains(currentIdp))
        {
            logger.LogInformation("Showing login: User is logged in with IdP: {IdP}, but IdP not in client restriction list.", currentIdp);
            context.Response = new InteractionResponse(context)
            {
                IsLogin = true
            };

            return;
        }

        // check client's user SSO timeout
        if (context.ClientParameters.Client.UserSsoLifetime.HasValue)
        {
            var authTimeEpoch = principal.GetAuthenticationTimeEpoch();
            var nowEpoch = time.GetUtcNow().ToUnixTimeSeconds();

            var diff = nowEpoch - authTimeEpoch;
            if (diff > context.ClientParameters.Client.UserSsoLifetime.Value)
            {
                logger.LogInformation("Showing login: User's auth session duration: {SessionDuration} exceeds client's user SSO lifetime: {UserSsoLifetime}.", diff, context.ClientParameters.Client.UserSsoLifetime);

                logger.LogInformation("Showing login: User is logged in with IdP: {IdP}, but IdP not in client restriction list.", currentIdp);
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

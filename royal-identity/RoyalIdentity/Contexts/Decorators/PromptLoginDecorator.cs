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
    private readonly ISessionStateGenerator sessionStateGenerator;
    private readonly ILogger logger;
    private readonly TimeProvider time;

    public PromptLoginDecorator(
        IProfileService profileService,
        ISessionStateGenerator sessionStateGenerator,
        ILogger<PromptLoginDecorator> logger,
        TimeProvider? time = null)
    {
        this.profileService = profileService;
        this.sessionStateGenerator = sessionStateGenerator;
        this.logger = logger;
        this.time = time ?? TimeProvider.System;
    }

    public async Task Decorate(IWithPrompt context, Func<Task> next, CancellationToken ct)
    {
        context.ClientParameters.AssertHasClient();

        if (context.PromptModes.Contains(Oidc.PromptModes.Login) ||
            context.PromptModes.Contains(Oidc.PromptModes.SelectAccount))
        {
            logger.LogInformation(
                "Showing login: request contains prompt={PromptModes}", 
                context.PromptModes.ToSpaceSeparatedString());

            // remove prompt so when we redirect back in from login page
            // we won't think we need to force a prompt again
            context.Raw.Remove(Oidc.Authorize.Request.Prompt);

            RequireLogin(context, "The request requires user interaction.");

            return;
        }

        var principal = context.Subject ?? new ClaimsPrincipal(new ClaimsIdentity());
        var identity = principal.Identity ?? new ClaimsIdentity();

        var isUserActive = identity.IsAuthenticated &&
            await profileService.IsActiveAsync(
                principal,
                context.ClientParameters.Client,
                "AuthorizeEndpoint",
                ct);

        if (!isUserActive)
        {
            logger.LogInformation("Showing login: User is not authenticated or not active");

            RequireLogin(context, "The user is not authenticated or is no longer active.");

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

                RequireLogin(context, "The current authentication is not fresh enough.");

                return;
            }
        }

        // check local IdP restrictions
        if (currentIdp == Server.LocalIdentityProvider)
        {
            if (!context.ClientParameters.Client.EnableLocalLogin)
            {
                logger.LogInformation("Showing login: User logged in locally, but client does not allow local logins");

                RequireLogin(context, "The current authentication method is not allowed for this client.");

                return;
            }
        }
        // check external IdP restrictions if user not using local IdP
        else if (context.ClientParameters.Client.IdentityProviderRestrictions.Count is not 0 &&
            !context.ClientParameters.Client.IdentityProviderRestrictions.Contains(currentIdp))
        {
            logger.LogInformation("Showing login: User is logged in with IdP: {IdP}, but IdP not in client restriction list.", currentIdp);
            RequireLogin(context, "The current identity provider is not allowed for this client.");

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
                RequireLogin(context, "The client's single sign-on lifetime has expired.");

                return;
            }
        }

        await next();
    }

    private void RequireLogin(IWithPrompt context, string description)
    {
        if (context.PromptModes.Contains(Oidc.PromptModes.None))
        {
            if (context is AuthorizeContext authorizeContext)
            {
                context.Response = AuthorizeResponseFactory.Interaction(
                    sessionStateGenerator,
                    authorizeContext,
                    AuthorizeInteractionKind.Login,
                    description);
            }
            else
            {
                context.Error(Oidc.Authorize.Errors.LoginRequired, description);
            }

            return;
        }

        context.Response = new InteractionResponse(context)
        {
            IsLogin = true
        };
    }
}

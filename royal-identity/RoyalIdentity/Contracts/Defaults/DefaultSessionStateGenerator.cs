using RoyalIdentity.Authentication;
using RoyalIdentity.Contexts;

namespace RoyalIdentity.Contracts.Defaults;

public sealed class DefaultSessionStateGenerator : ISessionStateGenerator
{
    public string? GenerateSessionStateValue(AuthorizeContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!ShouldGenerate(
            context.Options.Endpoints.EnableCheckSessionEndpoint,
            context.Scopes.IsOpenId,
            context.HasValidatedRedirectUri))
        {
            return null;
        }

        context.AssertHasRedirectUri();
        context.ClientParameters.AssertHasClient();

        if (!SessionStateFormat.TryGetOrigin(context.RedirectUri, out var origin))
            return null;

        if (!CheckSessionStateManager.TryGetRequestState(context.HttpContext, out var userAgentState))
            return null;

        return SessionStateFormat.Create(context.ClientParameters.Client.Id, origin, userAgentState);
    }

    internal static bool ShouldGenerate(
        bool checkSessionEndpointEnabled,
        bool isOpenIdRequest,
        bool hasValidatedRedirectUri)
        => checkSessionEndpointEnabled && isOpenIdRequest && hasValidatedRedirectUri;
}

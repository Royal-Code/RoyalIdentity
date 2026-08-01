using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts;

namespace RoyalIdentity.Responses;

/// <summary>
/// Constructs the bounded set of Authentication Responses whose redirect target is already trusted.
/// </summary>
/// <remarks>
/// This factory deliberately does not own pre-validation failures. Callers must only use it after client and
/// redirect URI validation; those earlier failures retain their existing direct error transport.
/// </remarks>
internal static class AuthorizeResponseFactory
{
    public static AuthorizeResponse Success(
        ISessionStateGenerator sessionStateGenerator,
        AuthorizeContext context,
        string? code,
        string? identityToken = null,
        string? accessToken = null)
    {
        AssertTrustedRedirect(context);

        return new AuthorizeResponse(
            context,
            code,
            sessionStateGenerator.GenerateSessionStateValue(context),
            identityToken,
            accessToken);
    }

    public static AuthorizeErrorResponse CreateError(
        ISessionStateGenerator sessionStateGenerator,
        AuthorizeContext context,
        string error,
        string? errorDescription = null)
    {
        AssertTrustedRedirect(context);

        return new AuthorizeErrorResponse(
            context,
            error,
            errorDescription,
            sessionStateGenerator.GenerateSessionStateValue(context));
    }

    public static AuthorizeErrorResponse Interaction(
        ISessionStateGenerator sessionStateGenerator,
        AuthorizeContext context,
        AuthorizeInteractionKind kind,
        string? errorDescription = null)
    {
        var error = kind switch
        {
            AuthorizeInteractionKind.Login => Oidc.Authorize.Errors.LoginRequired,
            AuthorizeInteractionKind.Consent => Oidc.Authorize.Errors.ConsentRequired,
            AuthorizeInteractionKind.AccountSelection => Oidc.Authorize.Errors.AccountSelectionRequired,
            _ => Oidc.Authorize.Errors.InteractionRequired,
        };

        return CreateError(sessionStateGenerator, context, error, errorDescription);
    }

    private static void AssertTrustedRedirect(AuthorizeContext context)
    {
        context.ClientParameters.AssertHasClient();
        context.AssertHasRedirectUri();
    }
}

internal enum AuthorizeInteractionKind
{
    Other,
    Login,
    Consent,
    AccountSelection,
}

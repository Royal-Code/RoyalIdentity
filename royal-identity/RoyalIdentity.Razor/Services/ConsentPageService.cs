using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Models.Messages;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Models;
using RoyalIdentity.Razor.ViewModels;
using static RoyalIdentity.Options.Constants;
using static RoyalIdentity.Options.Constants.UI;

namespace RoyalIdentity.Razor.Services;

public class ConsentPageService(
    ISessionContextService sessionContext,
    IConsentService consentService,
    IMessageStore messageStore) : IConsentPageService
{
    public async Task<ConsentViewModel?> BuildViewModelAsync(string? returnUrl, CancellationToken ct)
    {
        var context = await sessionContext.GetAuthorizationContextAsync(returnUrl);
        if (context is null)
            return null;

        return BuildConsentViewModel(context);
    }

    public async Task<ConsentResult> ProcessConsentAsync(string realm, ConsentInputModel input, CancellationToken ct)
    {
        var context = await sessionContext.GetAuthorizationContextAsync(input.ReturnUrl);
        if (context is null)
        {
            var error = new ErrorMessage { ErrorDescription = $"No consent request matching request: {input.ReturnUrl}" };
            var errorId = await messageStore.WriteAsync(new Message<ErrorMessage>(error), ct);
            return new ConsentResult(ConsentResultType.Denied, Routes.BuildErrorUrl(errorId), ForceLoad: true);
        }

        // User denied consent: resume the authorize callback with the denial marker so the pipeline
        // emits access_denied back to the client. Do not persist any consent.
        if (input.Button == ConsentButtons.No)
        {
            var denyUrl = input.ReturnUrl.AddQueryString(Oidc.Routes.Params.ConsentDenied, "true");
            return new ConsentResult(ConsentResultType.Denied, denyUrl, ForceLoad: true);
        }

        var viewModel = BuildConsentViewModel(context);

        if (viewModel.AllowRememberConsent is false && input.RememberConsent)
            return new ConsentResult(ConsentResultType.ValidationError,
                ErrorMessage: "Client does not allow consent to be remembered.");

        foreach (var scope in viewModel.IdentityScopes)
        {
            var inputScope = input.IdentityScopesConsent.FirstOrDefault(s => s.Scope == scope.Name);
            if (scope.Required && inputScope?.Checked is not true)
                return new ConsentResult(ConsentResultType.ValidationError, ErrorMessage: "Required scope not granted.");
        }

        foreach (var scope in viewModel.Scopes)
        {
            var inputScope = input.ScopesConsent.FirstOrDefault(s => s.Scope == scope.Name);
            if (scope.Required && inputScope?.Checked is not true)
                return new ConsentResult(ConsentResultType.ValidationError, ErrorMessage: "Required scope not granted.");
        }

        var consentedScopes = input.IdentityScopesConsent
            .Concat(input.ScopesConsent)
            .Where(s => s.Checked)
            .Select(s => new ConsentedScope
            {
                Scope = s.Scope,
                Description = input.Description,
                CreationTime = DateTimeOffset.UtcNow,
                JustOnce = !input.RememberConsent
            })
            .ToList();

        await consentService.UpdateConsentAsync(context.User, context.Client, consentedScopes, ct);

        var returnUrl = Routes.BuildConsentedUrl(realm, input.ReturnUrl);
        return new ConsentResult(ConsentResultType.Granted, returnUrl, ForceLoad: true);
    }

    private static ConsentViewModel BuildConsentViewModel(Users.Contexts.AuthorizationContext context)
    {
        var resources = context.Resources;

        // Group the requested application access by resource server (Fase 7). Each group carries the
        // resource server's requested scopes and requested protected resources (audience-only). A
        // resource server may appear with only protected resources (resource-only request) and no scopes.
        var groups = resources.ResourceServers
            .Select(rs => new ResourceServerConsentModel
            {
                Name = rs.Name,
                DisplayName = rs.DisplayName,
                Description = rs.Description,
                Scopes = [.. resources.Scopes.Where(s => rs.Scopes.Any(rss => rss.Name == s.Name))],
                ProtectedResources = [.. resources.ProtectedResources
                    .Where(pr => rs.ProtectedResources.Any(rsp => rsp.ResourceUri == pr.ResourceUri))],
            })
            .ToArray();

        return new ConsentViewModel
        {
            ClientName = context.Client.Name,
            ClientUrl = context.Client.ClientUri,
            ClientLogoUrl = context.Client.LogoUri,
            AllowRememberConsent = context.Client.AllowRememberConsent,
            OfflineAccess = resources.OfflineAccess,
            IdentityScopes = [.. resources.IdentityScopes],
            ResourceServers = groups,
        };
    }
}

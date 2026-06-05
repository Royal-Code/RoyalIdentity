using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Models.Messages;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models;
using RoyalIdentity.Razor.ViewModels;
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

        foreach (var scope in viewModel.ApiScopes)
        {
            var inputScope = input.ApiScopesConsent.FirstOrDefault(s => s.Scope == scope.Name);
            if (scope.Required && inputScope?.Checked is not true)
                return new ConsentResult(ConsentResultType.ValidationError, ErrorMessage: "Required scope not granted.");
        }

        var consentedScopes = input.IdentityScopesConsent
            .Concat(input.ApiScopesConsent)
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
        return new ConsentViewModel
        {
            ClientName = context.Client.Name,
            ClientUrl = context.Client.ClientUri,
            ClientLogoUrl = context.Client.LogoUri,
            AllowRememberConsent = context.Client.AllowRememberConsent,
            IdentityScopes = context.Resources.IdentityResources,
            ApiScopes = context.Resources.ApiScopes
        };
    }
}

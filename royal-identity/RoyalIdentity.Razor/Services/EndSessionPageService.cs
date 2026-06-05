using RoyalIdentity.Contracts.Models.Messages;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Razor.ViewModels;
using RoyalIdentity.Users;
using static RoyalIdentity.Options.Constants.UI;

namespace RoyalIdentity.Razor.Services;

public class EndSessionPageService(
    ISignOutManager signOutManager,
    IMessageStore messageStore) : IEndSessionPageService
{
    public async Task<LogoutResult> BeginLogoutAsync(string? logoutId, CancellationToken ct)
    {
        var effectiveLogoutId = logoutId ?? await signOutManager.CreateLogoutIdAsync(ct);
        if (effectiveLogoutId is null)
        {
            var error = new ErrorMessage { Error = "invalid_request", ErrorDescription = "Logout Id is required" };
            var errorId = await messageStore.WriteAsync(new Message<ErrorMessage>(error), ct);
            return new LogoutResult(LogoutResultType.Error, Routes.BuildErrorUrl(errorId));
        }

        var message = await messageStore.ReadAsync<LogoutMessage>(effectiveLogoutId, ct);
        var model = message?.Data;

        if (model is null || model.ShowSignoutPrompt)
            return new LogoutResult(LogoutResultType.RequiresConfirmation, effectiveLogoutId);

        await messageStore.DeleteAsync(effectiveLogoutId, ct);
        var uri = await signOutManager.SignOutAsync(model, ct);
        return new LogoutResult(LogoutResultType.LoggedOut, uri.AbsoluteUri);
    }

    public async Task<LogoutResult> ConfirmLogoutAsync(string confirmedId, CancellationToken ct)
    {
        var message = await messageStore.ReadAsync<LogoutMessage>(confirmedId, ct);
        if (message?.Data is null)
        {
            var error = new ErrorMessage { Error = "invalid_request", ErrorDescription = "Logout Id is not found" };
            var errorId = await messageStore.WriteAsync(new Message<ErrorMessage>(error), ct);
            return new LogoutResult(LogoutResultType.Error, Routes.BuildErrorUrl(errorId));
        }

        await messageStore.DeleteAsync(confirmedId, ct);
        var uri = await signOutManager.SignOutAsync(message.Data, ct);
        return new LogoutResult(LogoutResultType.LoggedOut, uri.AbsoluteUri);
    }

    public async Task<LoggingOutViewModel?> BuildLoggingOutAsync(string? logoutId, CancellationToken ct)
    {
        if (logoutId is null)
            return null;

        var message = await messageStore.ReadAsync<LogoutCallbackMessage>(logoutId, ct);
        if (message is null)
            return null;

        var data = message.Data;
        return new LoggingOutViewModel
        {
            PostLogoutRedirectUri = data.PostLogoutRedirectUri,
            ClientName = data.ClientName,
            AutomaticRedirectAfterSignOut = data.AutomaticRedirectAfterSignOut,
            SignOutIframeUrl = data.SignOutIframeUrl
        };
    }
}

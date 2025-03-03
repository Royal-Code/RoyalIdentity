using Microsoft.AspNetCore.Http;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Users;

namespace RoyalIdentity.Razor.Services;

public class IdentityUserManager
{
    private readonly IStorage storage;
    private readonly IdentityRedirectManager redirectManager;

    public IdentityUserManager(IStorage storage, IdentityRedirectManager redirectManager)
    {
        this.storage = storage;
        this.redirectManager = redirectManager;
    }

    public async Task<IdentityUser> GetRequiredUserAsync(HttpContext context)
    {
        var realm = context.GetCurrentRealm();

        IdentityUser? user = null;
        var userName = context.User.Identity?.Name;
        if (userName is not null)
            user = await storage.GetUserStore(realm).GetUserAsync(userName);

        if (user is null)
            redirectManager.RedirectToWithStatus("Account/InvalidUser", $"Error: Unable to load user with user name '{userName}'.", context);

        return user;
    }
}

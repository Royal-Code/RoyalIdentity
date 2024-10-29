using RoyalIdentity.Users;
using RoyalIdentity.Users.Contracts;

namespace RoyalIdentity.Server.Services;

public class IdentityUserManager
{
    private readonly IUserStore userStore;
    private readonly IdentityRedirectManager redirectManager;

    public IdentityUserManager(IUserStore userStore, IdentityRedirectManager redirectManager)
    {
        this.userStore = userStore;
        this.redirectManager = redirectManager;
    }

    public async Task<IdentityUser> GetRequiredUserAsync(HttpContext context)
    {
        IdentityUser? user = null;
        var userName = context.User.Identity?.Name;
        if (userName is not null)
            user = await userStore.GetUserAsync(userName);

        if (user is null)
            redirectManager.RedirectToWithStatus("Account/InvalidUser", $"Error: Unable to load user with Username '{userName}'.", context);

        return user;
    }
}

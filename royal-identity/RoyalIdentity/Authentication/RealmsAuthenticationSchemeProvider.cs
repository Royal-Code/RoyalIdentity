using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace RoyalIdentity.Authentication;

public class RealmsAuthenticationSchemeProvider : AuthenticationSchemeProvider
{
    public RealmsAuthenticationSchemeProvider(IOptions<AuthenticationOptions> options) : base(options)
    { }

    protected RealmsAuthenticationSchemeProvider(
        IOptions<AuthenticationOptions> options,
        IDictionary<string, AuthenticationScheme> schemes) : base(options, schemes)
    { }

    public override async Task<AuthenticationScheme?> GetSchemeAsync(string name)
    {
        return name.StartsWith(RealmAuthenticationNamePrefix)
            ? new AuthenticationScheme(name, name, typeof(CookieAuthenticationHandler))
            : await base.GetSchemeAsync(name);
    }

    public override async Task<IEnumerable<AuthenticationScheme>> GetAllSchemesAsync()
    {
        var baseSchemes = await base.GetAllSchemesAsync();

        return baseSchemes.Concat([new AuthenticationScheme(
            ServerAuthenticationScheme,
            ServerAuthenticationName,
            typeof(CookieAuthenticationHandler))]);
    }
}

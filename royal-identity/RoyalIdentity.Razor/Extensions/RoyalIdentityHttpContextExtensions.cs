namespace Microsoft.AspNetCore.Http;

public static class RoyalIdentityHttpContextExtensions
{
    public static bool IsAccountPages(this HttpContext httpContext)
    {
        if (httpContext.Request.Path.Value is string path &&
            httpContext.Request.RouteValues.TryGetValue("realm", out var realmValue) &&
            realmValue is string realm)
        {
            var len = realm.Length + 2;

            return path.Length > len && path[len..].StartsWith("account/", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }
}

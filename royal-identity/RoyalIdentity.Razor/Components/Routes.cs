using RoyalIdentity.Extensions;

namespace RoyalIdentity.Razor.Components;

#pragma warning disable S3218 // Inner class names should not shadow outer class names

public static class Routes
{
    public const string Login = $"/{{realm}}/{Names.Account}/{Names.Login}";

    public const string SignedIn = $"/{{realm}}/{Names.Account}/{Names.SignedIn}";

    public const string SelectDomain = $"/{Names.Account}/{Names.Domain}";

    public const string Consent = $"/{{realm}}/{Names.Account}/{Names.Consent}";

    public const string Consented = $"/{{realm}}/{Names.Account}/{Names.Consented}";

    public const string Logout = $"/{{realm}}/{Names.Account}/{Names.Logout}";

    public const string LoggingOut = $"/{{realm}}/{Names.Account}/{Names.LoggingOut}";

    public const string LoggedOut = $"/{{realm}}/{Names.Account}/{Names.LoggedOut}";

    public const string Profile = $"/{{realm}}/{Names.Account}/{Names.Profile}";

    public const string Error = "/error";

    private static class Names
    {
        public const string Account = "account";
        public const string Login = "login";
        public const string SignedIn = "signedin";
        public const string Domain = "domain";
        public const string Consent = "consent";
        public const string Consented = "consented";
        public const string Logout = "logout";
        public const string LoggingOut = "logout/processing";
        public const string LoggedOut = "logout/done";
        public const string Profile = "profile";
    }

    public static class Params
    {
        public const string ReturnUrl = "returnUrl";

        public const string LogoutId = "logoutId";

        public const string ErrorId = "errorId";
    }

    public static string BuildLoginUrl(string realm, string? returnUrl)
        => $"/{realm}/{Names.Account}/{Names.Login}".AddQueryString(Params.ReturnUrl, returnUrl);

    public static string BuildSignedInUrl(string realm, string? returnUrl)
        => $"/{realm}/{Names.Account}/{Names.SignedIn}".AddQueryString(Params.ReturnUrl, returnUrl);

    public static string BuildConsentUrl(string realm, string? returnUrl)
        => $"/{realm}/{Names.Account}/{Names.Consent}".AddQueryString(Params.ReturnUrl, returnUrl);

    public static string BuildConsentedUrl(string realm, string? returnUrl)
        => $"/{realm}/{Names.Account}/{Names.Consented}".AddQueryString(Params.ReturnUrl, returnUrl);

    public static string BuildLogoutUrl(string realm, string? logoutId)
        => $"/{realm}/{Names.Account}/{Names.Logout}".AddQueryString(Params.LogoutId, logoutId);

    public static string BuildLoggingOutUrl(string realm, string? logoutId)
        => $"/{realm}/{Names.Account}/{Names.LoggingOut}".AddQueryString(Params.LogoutId, logoutId);

    public static string BuildLoggedOutUrl(string realm)
        => $"/{realm}/{Names.Account}/{Names.LoggedOut}";

    public static string BuildProfileUrl(string realm)
        => $"/{realm}/{Names.Account}/{Names.Profile}";

    public static string BuildErrorUrl(string errorId) 
        => Error.AddQueryString(Params.ErrorId, errorId);
}

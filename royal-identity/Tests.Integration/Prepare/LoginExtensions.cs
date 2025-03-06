using RoyalIdentity.Extensions;
using RoyalIdentity.Responses.HttpResults;
using System.Text.Json;
using System.Web;

namespace Tests.Integration.Prepare;

internal static class LoginExtensions
{
    public static async Task LoginAsync(this HttpClient client, string username, string password, string reaml = "demo")
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = username,
            ["password"] = password
        });

        var response = await client.PostAsync($"{reaml}/test/account/login", content);
        response.EnsureSuccessStatusCode();
    }

    public static async Task LoginAliceAsync(this HttpClient client)
    {
        await LoginAsync(client, "alice", "alice");
    }

    public static async Task LoginBobAsync(this HttpClient client)
    {
        await LoginAsync(client, "bob", "bob");
    }

    public static async Task<HttpResponseMessage> LogoutAsync(this HttpClient client, string reaml = "demo")
    {
        var response = await client.GetAsync($"{reaml}/test/account/logout");
        response.EnsureSuccessStatusCode();
        return response;
    }

    public static async Task<TokenEndpointParameters> GetTokensAsync(
        this HttpClient client,
        string clientId = "demo_client",
        string scope = "openid profile offline_access",
        string reaml = "demo")
    {
        var path = $"{reaml}/test/account/token"
            .AddQueryString("client_id", clientId)
            .AddQueryString("scope", scope);

        var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<TokenEndpointParameters>(json)!;
    }

    public static async Task<string?> GetAuthorizeAsync(
        this HttpClient client,
        string clientId = "demo_client",
        string scope = "openid profile offline_access",
        string redirectUri = "http://localhost:5000/callback",
        string reaml = "demo")
    {
        var path = Oidc.Routes.BuildAuthorizeUrl(reaml)
            .AddQueryString("client_id", clientId)
            .AddQueryString("response_type", "code")
            .AddQueryString("response_mode", "query")
            .AddQueryString("scope", scope)
            .AddQueryString("redirect_uri", redirectUri)
            .AddQueryString("code_challenge", "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")
            .AddQueryString("code_challenge_method", "S256");

        var response = await client.GetAsync(path);

        var location = response.Headers.Location;

        if (location is null)
            return null;

        var parameters = HttpUtility.ParseQueryString(location.Query);
        return parameters["code"];
    }
}
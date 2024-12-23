using RoyalIdentity.Extensions;
using RoyalIdentity.Responses.HttpResults;
using System.Text.Json;

namespace Tests.Integration.Prepare;

internal static class LoginExtensions
{
    public static async Task LoginAsync(this HttpClient client, string username, string password)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = username,
            ["password"] = password
        });

        var response = await client.PostAsync("account/login", content);
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

    public static async Task<TokenEndpointValues> GetTokenAsync(
        this HttpClient client,
        string clientId = "demo_client",
        string scope = "openid profile offline_access")
    {
        var path = "account/token".AddQueryString("client_id", clientId).AddQueryString("scope", scope);

        var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<TokenEndpointValues>(json)!;
    }
}
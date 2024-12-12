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

    public static async Task LoginbobAsync(this HttpClient client)
    {
        await LoginAsync(client, "bob", "bob");
    }
}

using RoyalIdentity.Events;
using RoyalIdentity.Users;
using Tests.Integration.Prepare;

namespace Tests.Integration.Characterization;

/// <summary>
/// Fase 7 (plan-users-edge-session.md) — login events carry audit data without leaking the internal
/// authentication failure reason to the UI message.
/// </summary>
public class LoginEventCharacterizationTests : IClassFixture<EventCapturingAppFactory>
{
    private readonly EventCapturingAppFactory factory;

    public LoginEventCharacterizationTests(EventCapturingAppFactory factory)
    {
        this.factory = factory;
        this.factory.EventCapture.Reset();
    }

    [Fact]
    public async Task LoginFailureEvent_PreservesInternalReason_ForAudit()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync("demo/test/account/login", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["username"] = "alice",
                ["password"] = "wrong-password"
            }));

        Assert.False(response.IsSuccessStatusCode);

        var evt = Assert.Single(factory.EventCapture.Events.OfType<UserLoginFailureEvent>());
        Assert.Equal("alice", evt.Username);
        Assert.Equal(MemoryStorage.DemoRealm.Id, evt.RealmId);
        Assert.Equal(AuthenticationFailureReason.InvalidCredentials, evt.Reason);
        Assert.Equal("Invalid username or password", evt.Message);
    }

    [Fact]
    public async Task LoginSuccessEvent_IsRealmScoped()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync("demo/test/account/login", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["username"] = "alice",
                ["password"] = "alice"
            }));

        response.EnsureSuccessStatusCode();

        var evt = Assert.Single(factory.EventCapture.Events.OfType<UserLoginSuccessEvent>());
        Assert.Equal("alice", evt.Username);
        Assert.Equal(MemoryStorage.AliceSubjectId, evt.SubjectId);
        Assert.Equal(MemoryStorage.DemoRealm.Id, evt.RealmId);
    }
}

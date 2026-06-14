using RoyalIdentity.Users;

namespace Tests.Identity.Users;

/// <summary>
/// Fase 3 (plan-users-edge-session.md) — the new <see cref="UserSessionClient"/> type deduplicates by
/// <c>ClientId</c> (pontos1 §1), so a session's client set keeps one entry per client regardless of the
/// seen-at timestamps.
/// </summary>
public class UserSessionClientTests
{
    [Fact]
    public void Equality_IsByClientId_IgnoringTimestamps()
    {
        var a = new UserSessionClient("client-1", new DateTime(2026, 1, 1), new DateTime(2026, 1, 1));
        var b = new UserSessionClient("client-1", new DateTime(2030, 6, 30), new DateTime(2030, 7, 1));

        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentClientId_AreNotEqual()
    {
        var a = new UserSessionClient("client-1", DateTime.UnixEpoch, DateTime.UnixEpoch);
        var b = new UserSessionClient("client-2", DateTime.UnixEpoch, DateTime.UnixEpoch);

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void HashSet_DeduplicatesByClientId()
    {
        var set = new HashSet<UserSessionClient>
        {
            new("client-1", DateTime.UnixEpoch, DateTime.UnixEpoch),
            new("client-1", DateTime.UtcNow, DateTime.UtcNow),
            new("client-2", DateTime.UnixEpoch, DateTime.UnixEpoch),
        };

        Assert.Equal(2, set.Count);
        Assert.Contains(set, c => c.ClientId == "client-1");
        Assert.Contains(set, c => c.ClientId == "client-2");
    }
}

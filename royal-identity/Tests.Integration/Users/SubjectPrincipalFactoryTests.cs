using System.Security.Claims;
using RoyalIdentity.Users;
using RoyalIdentity.Users.Defaults;

namespace Tests.Integration.Users;

/// <summary>
/// Fase 7 (plan-users-edge-session.md) — the session principal carries ONLY the minimal protocol claims
/// (<c>sub</c>, <c>name</c>, <c>auth_time</c>, <c>sid</c>, <c>idp</c>, <c>amr</c>). Roles and profile claims
/// must NOT be in the cookie principal (ADR-014 §2.8); they flow via IProfileService for token/userinfo.
/// </summary>
public class SubjectPrincipalFactoryTests
{
    [Fact]
    public void Create_EmitsOnlyMinimalSessionClaims()
    {
        var factory = new DefaultSubjectPrincipalFactory();
        var subject = new Subject("sub-123", "Alice", IsActive: true);
        var session = new UserSession
        {
            Id = "sid-abc",
            SubjectId = "sub-123",
            AuthenticationMethod = "pwd",
            IdentityProvider = "local",
            StartedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };

        var principal = factory.Create(subject, session);

        var types = principal.Claims.Select(c => c.Type).ToHashSet();
        Assert.Equal(
            new HashSet<string>
            {
                JwtRegisteredClaimNames.Sub,
                JwtRegisteredClaimNames.Name,
                JwtRegisteredClaimNames.AuthTime,
                JwtRegisteredClaimNames.Sid,
                Jwt.ClaimTypes.IdentityProvider,
                JwtRegisteredClaimNames.Amr,
            },
            types);

        Assert.Equal("sub-123", principal.FindFirstValue(JwtRegisteredClaimNames.Sub));
        Assert.Equal("sid-abc", principal.FindFirstValue(JwtRegisteredClaimNames.Sid));

        // no roles / profile claims leak into the session principal
        Assert.DoesNotContain(principal.Claims, c => c.Type == Jwt.ClaimTypes.Role);
        Assert.DoesNotContain(principal.Claims, c => c.Type == "email");
    }
}

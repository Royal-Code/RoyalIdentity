using System.Security.Claims;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using RoyalIdentity.Users.Contracts;

namespace RoyalIdentity.Users.Defaults;

/// <summary>
/// Builds the minimal session principal from a <see cref="Subject"/> + <see cref="UserSession"/>
/// (ADR-014 §2.8): only the protocol claims <c>sub</c>, <c>name</c>, <c>auth_time</c>, <c>sid</c>,
/// <c>idp</c>, <c>amr</c>. Roles and profile claims are NOT placed in the cookie/session principal — they
/// flow through <see cref="IProfileService"/> / <see cref="IUserPropertyProvider"/> for token/userinfo.
/// </summary>
public sealed class DefaultSubjectPrincipalFactory : ISubjectPrincipalFactory
{
    public ClaimsPrincipal Create(Subject subject, UserSession session)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, subject.SubjectId),
            new(JwtRegisteredClaimNames.Name, subject.DisplayName),
            new(JwtRegisteredClaimNames.AuthTime, new DateTimeOffset(session.StartedAt).ToUnixTimeSeconds().ToString()),
            new(JwtRegisteredClaimNames.Sid, session.Id),
            new(Jwt.ClaimTypes.IdentityProvider, session.IdentityProvider),
            new(JwtRegisteredClaimNames.Amr, session.AuthenticationMethod),
        };

        return new ClaimsPrincipal(claims.CreateIdentity());
    }
}

using System.Security.Claims;

namespace RoyalIdentity.Users.Contracts;

/// <summary>
/// Builds the minimal session principal from a subject + session: <c>sub</c>, <c>name</c>,
/// <c>auth_time</c>, <c>sid</c>, <c>idp</c>, <c>amr</c>. It does NOT emit roles/profile claims — those
/// flow through <c>IProfileService</c> / <c>IUserClaimsProvider</c> (token/userinfo), not the cookie
/// (ADR-014 §2.8). Needs no realm (reads it from the subject/session context).
/// </summary>
public interface ISubjectPrincipalFactory
{
    /// <summary>Creates the minimal session <see cref="ClaimsPrincipal"/>.</summary>
    ClaimsPrincipal Create(Subject subject, UserSession session);
}

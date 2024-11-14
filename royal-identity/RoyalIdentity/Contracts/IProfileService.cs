using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Models;
using System.Security.Claims;

namespace RoyalIdentity.Contracts;

/// <summary>
/// This interface allows IdentityServer to connect to your user and profile store.
/// </summary>
public interface IProfileService
{
    /// <summary>
    /// This method is called whenever claims about the user are requested (e.g. during token creation or via the userinfo endpoint)
    /// </summary>
    /// <param name="request">The data request.</param>
    /// <returns></returns>
    ValueTask GetProfileDataAsync(ProfileDataRequest request);

    /// <summary>
    /// This method gets called whenever identity server needs to determine if the user is valid or active (e.g. if the user's account has been deactivated since they logged in).
    /// (e.g. during token issuance or validation).
    /// </summary>
    /// <param name="subject">The subject, user.</param>
    /// <param name="client">The client that request.</param>
    /// <param name="caller">The caller context.</param>
    /// <returns>
    /// <para>
    ///     A value indicating whether the subject is active and can recieve tokens.
    /// </para>
    /// <para>
    ///     <c>true</c> if the subject is active; otherwise, <c>false</c>.
    /// </para>
    /// </returns>
    ValueTask<bool> IsActiveAsync(ClaimsPrincipal subject, Client client, string caller, CancellationToken ct);
}

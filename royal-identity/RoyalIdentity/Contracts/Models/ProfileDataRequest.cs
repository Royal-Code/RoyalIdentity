using RoyalIdentity.Models;
using System.Security.Claims;
using RoyalIdentity.Contexts;
using RoyalIdentity.Utils;

namespace RoyalIdentity.Contracts.Models;

/// <summary>
/// Class describing the profile data request
/// </summary>
public class ProfileDataRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileDataRequest" /> class.
    /// </summary>
    /// <param name="requestedResources">The requested resources.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="client">The client.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="requestedClaimTypes">The requested claim types.</param>
    public ProfileDataRequest(
        Resources requestedResources,
        ClaimsPrincipal subject,
        Client client,
        string caller,
        string identityType,
        IReadOnlyList<string> requestedClaimTypes)
    {
        RequestedResources = requestedResources;
        Subject = subject;
        Client = client;
        Caller = caller;
        IdentityType = identityType;
        RequestedClaimTypes = requestedClaimTypes;
    }

    /// <summary>
    /// Gets or sets the subject.
    /// </summary>
    /// <value>
    /// The subject.
    /// </value>
    public ClaimsPrincipal Subject { get; }

    /// <summary>
    /// Gets or sets the requested resources (if available).
    /// </summary>
    /// <value>
    /// The resources.
    /// </value>
    public Resources RequestedResources { get; }

    /// <summary>
    /// Gets or sets the requested claim types.
    /// </summary>
    /// <value>
    /// The requested claim types.
    /// </value>
    public IReadOnlyList<string> RequestedClaimTypes { get; }

    /// <summary>
    /// Gets or sets the client id.
    /// </summary>
    /// <value>
    /// The client id.
    /// </value>
    public Client Client { get; }

    /// <summary>
    /// Gets or sets the caller.
    /// </summary>
    /// <value>
    /// The caller.
    /// </value>
    public string Caller { get; }

    /// <summary>
    /// Gets or sets the identity type.
    /// </summary>
    /// <value>
    /// The type of identity.
    /// </value>
    public string IdentityType { get; }

    /// <summary>
    /// Gets or sets the issued claims.
    /// </summary>
    /// <value>
    /// The issued claims.
    /// </value>
    public HashSet<Claim> IssuedClaims { get; } = new HashSet<Claim>(new ClaimComparer());
}
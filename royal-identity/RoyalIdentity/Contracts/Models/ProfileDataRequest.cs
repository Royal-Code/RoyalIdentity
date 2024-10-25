using RoyalIdentity.Models;
using System.Security.Claims;
using RoyalIdentity.Contexts;

namespace RoyalIdentity.Contracts.Models;

/// <summary>
/// Class describing the profile data request
/// </summary>
public class ProfileDataRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileDataRequest" /> class.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="requestedResources">The requested resources.</param>
    /// <param name="subject">The subject.</param>
    /// <param name="client">The client.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="requestedClaimTypes">The requested claim types.</param>
    public ProfileDataRequest(
        IEndpointContextBase context,
        Resources requestedResources,
        ClaimsPrincipal subject,
        Client client,
        string caller,
        IEnumerable<string> requestedClaimTypes)
    {
        Context = context;
        RequestedResources = requestedResources;
        Subject = subject;
        Client = client;
        Caller = caller;
        RequestedClaimTypes = requestedClaimTypes;
    }

    /// <summary>
    /// Gets or sets the validatedRequest.
    /// </summary>
    /// <value>
    /// The validatedRequest.
    /// </value>
    public IEndpointContextBase Context { get; }

    /// <summary>
    /// Gets or sets the requested resources (if available).
    /// </summary>
    /// <value>
    /// The resources.
    /// </value>
    public Resources RequestedResources { get; }

    /// <summary>
    /// Gets or sets the subject.
    /// </summary>
    /// <value>
    /// The subject.
    /// </value>
    public ClaimsPrincipal Subject { get; }

    /// <summary>
    /// Gets or sets the requested claim types.
    /// </summary>
    /// <value>
    /// The requested claim types.
    /// </value>
    public IEnumerable<string> RequestedClaimTypes { get; }

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
    /// Gets or sets the issued claims.
    /// </summary>
    /// <value>
    /// The issued claims.
    /// </value>
    public List<Claim> IssuedClaims { get; } = [];
}
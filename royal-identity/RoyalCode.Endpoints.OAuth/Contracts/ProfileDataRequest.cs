using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Models;
using System.Security.Claims;

namespace RoyalIdentity.Contracts;

/// <summary>
/// Class describing the profile data request
/// </summary>
public class ProfileDataRequest
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileDataRequest"/> class.
    /// </summary>
    public ProfileDataRequest()
    { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileDataRequest" /> class.
    /// </summary>
    /// <param name="subject">The subject.</param>
    /// <param name="client">The client.</param>
    /// <param name="caller">The caller.</param>
    /// <param name="requestedClaimTypes">The requested claim types.</param>
    public ProfileDataRequest(ClaimsPrincipal subject, Client client, string caller, IEnumerable<string> requestedClaimTypes)
    {
        Subject = subject ?? throw new ArgumentNullException(nameof(subject));
        Client = client ?? throw new ArgumentNullException(nameof(client));
        Caller = caller ?? throw new ArgumentNullException(nameof(caller));
        RequestedClaimTypes = requestedClaimTypes ?? throw new ArgumentNullException(nameof(requestedClaimTypes));
    }

    /// <summary>
    /// Gets or sets the validatedRequest.
    /// </summary>
    /// <value>
    /// The validatedRequest.
    /// </value>
    public IContextBase Context { get; set; }

    /// <summary>
    /// Gets or sets the subject.
    /// </summary>
    /// <value>
    /// The subject.
    /// </value>
    public ClaimsPrincipal Subject { get; set; }

    /// <summary>
    /// Gets or sets the requested claim types.
    /// </summary>
    /// <value>
    /// The requested claim types.
    /// </value>
    public IEnumerable<string> RequestedClaimTypes { get; set; }

    /// <summary>
    /// Gets or sets the client id.
    /// </summary>
    /// <value>
    /// The client id.
    /// </value>
    public Client Client { get; set; }

    /// <summary>
    /// Gets or sets the caller.
    /// </summary>
    /// <value>
    /// The caller.
    /// </value>
    public string Caller { get; set; }

    /// <summary>
    /// Gets or sets the requested resources (if available).
    /// </summary>
    /// <value>
    /// The resources.
    /// </value>
    public Resources RequestedResources { get; set; }

    /// <summary>
    /// Gets or sets the issued claims.
    /// </summary>
    /// <value>
    /// The issued claims.
    /// </value>
    public List<Claim> IssuedClaims { get; set; } = new List<Claim>();
}
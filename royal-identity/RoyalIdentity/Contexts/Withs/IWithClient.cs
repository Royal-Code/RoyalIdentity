using RoyalIdentity.Models;
using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;

namespace RoyalIdentity.Contexts.Withs;

public interface IWithClient : IEndpointContextBase
{
    public Client? Client { get; }

    public string? ClientId { get; set; }

    /// <summary>
    /// Gets or sets the client claims for the current request.
    /// This value is initally read from the client configuration but can be modified in the request pipeline
    /// </summary>
    public HashSet<Claim> ClientClaims { get; set; }

    [MemberNotNull(nameof(Client), nameof(ClientId))]
    public void AssertHasClient();

    public void SetClient(Client client);
}

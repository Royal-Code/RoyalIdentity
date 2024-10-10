using RoyalIdentity.Models;
using System.Diagnostics.CodeAnalysis;

namespace RoyalIdentity.Contexts.Withs;

public interface IWithClient : IEndpointContextBase
{
    public Client? Client { get; set; }

    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }

    [MemberNotNull(nameof(Client), nameof(ClientId))]
    public void AssertHasClient();
}

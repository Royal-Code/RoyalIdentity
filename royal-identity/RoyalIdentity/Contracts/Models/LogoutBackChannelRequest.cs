using RoyalIdentity.Models;

namespace RoyalIdentity.Contracts.Models;

#nullable disable // POCO

public class LogoutBackChannelRequest
{
    public required Realm Realm { get; set; }

    public string Uri { get; set; }

    public string Issuer { get; set; }

    public string Subject { get; set; }

    public string Audience { get; set; }

    public string SessionId { get; set; }

    public string ClientId { get; set; }

    public bool RequireSessionId { get; set; }
}
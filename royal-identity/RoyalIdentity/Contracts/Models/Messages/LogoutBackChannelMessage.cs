namespace RoyalIdentity.Contracts.Models.Messages;

#nullable disable // POCO

public class LogoutBackChannelMessage
{
    public string Uri { get; set; }

    public string Issuer { get; set; }

    public string Subject { get; set; }

    public string Audience { get; set; }

    public string SessionId { get; set; }

    public bool RequireSessionId { get; set; }
}
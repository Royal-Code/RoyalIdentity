namespace RoyalIdentity.Contracts.Models.Messages;

public class ErrorMessage
{
    public string? Error { get; set; }

    public string? ErrorDescription { get; set; }

    public string? RequestId { get; set; }
}

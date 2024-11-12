namespace RoyalIdentity.Contracts;

public class EvaluatedCredential
{
    public EvaluatedCredential(string type, bool isValid, object? credential = null, string? confirmation = null)
    {
        Type = type;
        IsValid = isValid;
        Credential = credential;
        Confirmation = confirmation;
    }

    public string Type { get; }

    public bool IsValid { get; }

    public object? Credential { get; }

    /// <summary>
    /// Gets the value of the confirmation method (will become the cnf claim). Must be a JSON object.
    /// </summary>
    /// <value>
    /// The confirmation.
    /// </value>
    public string? Confirmation { get; }
}
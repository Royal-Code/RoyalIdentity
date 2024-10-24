namespace RoyalIdentity.Models;

/// <summary>
/// Validation error class.
/// </summary>
public class ValidationError
{
    /// <summary>
    /// Gets or sets the error.
    /// </summary>
    /// <value>
    /// The error.
    /// </value>
    public required string Error { get; init; }

    /// <summary>
    /// Gets or sets the error description.
    /// </summary>
    /// <value>
    /// The error description.
    /// </value>
    public string? ErrorDescription { get; init; }

    /// <summary>
    /// Gets or sets the error uri.
    /// </summary>
    public string? ErrorUri { get; init; }
}
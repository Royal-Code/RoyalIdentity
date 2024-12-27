using System.Text.Json.Serialization;

namespace RoyalIdentity.Models;

/// <summary>
/// Validation error details.
/// </summary>
public class ErrorDetails
{
    /// <summary>
    /// Gets or sets the error.
    /// </summary>
    /// <value>
    /// The error.
    /// </value>
    [JsonPropertyName("error")]
    public required string Error { get; init; }

    /// <summary>
    /// Gets or sets the error description.
    /// </summary>
    /// <value>
    /// The error description.
    /// </value>
    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; init; }

    /// <summary>
    /// Gets or sets the error uri.
    /// </summary>
    [JsonPropertyName("error_uri")]
    public string? ErrorUri { get; init; }
}
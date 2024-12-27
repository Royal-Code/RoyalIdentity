using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace RoyalIdentity.Endpoints.Abstractions;

/// <summary>
/// The error response parameters.
/// </summary>
public sealed class ErrorResponseParameters
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


[JsonSerializable(typeof(ErrorResponseParameters))]
[JsonSourceGenerationOptions(WriteIndented = true)]
public partial class ErrorResponseJsonContenxt : JsonSerializerContext { }
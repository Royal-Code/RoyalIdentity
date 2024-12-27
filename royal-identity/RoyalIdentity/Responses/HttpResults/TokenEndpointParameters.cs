using System.Text.Json.Serialization;

namespace RoyalIdentity.Responses.HttpResults;

public class TokenEndpointParameters
{
    [JsonPropertyName("access_token")]
    public required string AccessToken { get; init; }

    [JsonPropertyName("token_type")]
    public required string TokenType { get; init; }

    [JsonPropertyName("expires_in")]
    public required int ExpiresIn { get; init; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; init; }

    [JsonPropertyName("id_token")]
    public string? IdentityToken { get; init; }

    [JsonPropertyName("scope")]
    public string? Scope { get; init; }

    [JsonExtensionData]
    public Dictionary<string, object>? Custom { get; init; }
}

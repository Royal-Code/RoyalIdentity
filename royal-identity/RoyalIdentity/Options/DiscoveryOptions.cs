// This file contains material derived from IdentityServer4 and/or IdentityModel.
// Original component copyrights remain with Brock Allen, Dominick Baier, and/or Duende Software.
// Licensed under Apache License 2.0; see LICENSES/Apache-2.0.txt and THIRD-PARTY-NOTICES.md.
// Modified by RoyalIdentity contributors for the RoyalIdentity rearchitecture.
//
namespace RoyalIdentity.Options;

using System.Text.Json;

/// <summary>
/// Options class to configure discovery endpoint
/// </summary>
public class DiscoveryOptions
{
    public DiscoveryOptions()
    {
    }

    public DiscoveryOptions(DiscoveryOptions other)
    {
        ShowEndpoints = other.ShowEndpoints;
        ShowKeySet = other.ShowKeySet;
        ShowIdentityScopes = other.ShowIdentityScopes;
        ShowScopes = other.ShowScopes;
        ShowClaims = other.ShowClaims;
        ShowResponseTypes = other.ShowResponseTypes;
        ShowResponseModes = other.ShowResponseModes;
        ShowGrantTypes = other.ShowGrantTypes;
        ShowExtensionGrantTypes = other.ShowExtensionGrantTypes;
        ShowTokenEndpointAuthenticationMethods = other.ShowTokenEndpointAuthenticationMethods;
        ExpandRelativePathsInCustomEntries = other.ExpandRelativePathsInCustomEntries;
        ResponseCacheInterval = other.ResponseCacheInterval;
        CustomEntries = other.CustomEntries.ToDictionary(
            entry => entry.Key,
            entry => CloneJsonValue(entry.Value)!,
            StringComparer.Ordinal);

        SupportedSubjectTypes.Clear();
        foreach (var value in other.SupportedSubjectTypes)
        {
            SupportedSubjectTypes.Add(value);
        }

        SupportedDisplayModes.Clear();
        foreach (var value in other.SupportedDisplayModes)
        {
            SupportedDisplayModes.Add(value);
        }

        SupportedPromptModes.Clear();
        foreach (var value in other.SupportedPromptModes)
        {
            SupportedPromptModes.Add(value);
        }

        SupportedTokenTypeHints.Clear();
        foreach (var value in other.SupportedTokenTypeHints)
        {
            SupportedTokenTypeHints.Add(value);
        }
    }

    /// <summary>
    /// Show endpoints
    /// </summary>
    public bool ShowEndpoints { get; set; } = true;

    /// <summary>
    /// Show signing keys
    /// </summary>
    public bool ShowKeySet { get; set; } = true;

    /// <summary>
    /// Show identity scopes
    /// </summary>
    public bool ShowIdentityScopes { get; set; } = true;

    /// <summary>
    /// Show scopes
    /// </summary>
    public bool ShowScopes { get; set; } = true;

    /// <summary>
    /// Show identity claims
    /// </summary>
    public bool ShowClaims { get; set; } = true;

    /// <summary>
    /// Show response types
    /// </summary>
    public bool ShowResponseTypes { get; set; } = true;

    /// <summary>
    /// Show response modes
    /// </summary>
    public bool ShowResponseModes { get; set; } = true;

    /// <summary>
    /// Show standard grant types
    /// </summary>
    public bool ShowGrantTypes { get; set; } = true;

    /// <summary>
    /// Show custom grant types
    /// </summary>
    public bool ShowExtensionGrantTypes { get; set; } = true;

    /// <summary>
    /// Show token endpoint authentication methods
    /// </summary>
    public bool ShowTokenEndpointAuthenticationMethods { get; set; } = true;

    /// <summary>
    /// Turns relative paths that start with ~/ into absolute paths
    /// </summary>
    public bool ExpandRelativePathsInCustomEntries { get; set; } = true;

    /// <summary>
    /// Sets the max age value of the cache control header (in seconds) of the HTTP response.
    /// <br />
    /// This gives clients a hint how often they should refresh their cached copy of the discovery document.
    /// <br />
    /// If set to 0 no-cache headers will be set. 
    /// <br />
    /// Defaults to null, which does not set the header.
    /// </summary>
    /// <value>
    /// The cache interval in seconds.
    /// </value>
    public int? ResponseCacheInterval { get; set; } = null;

    /// <summary>
    /// Adds custom entries to the discovery document
    /// </summary>
    public Dictionary<string, object> CustomEntries { get; set; } = new();

    public HashSet<string> SupportedSubjectTypes { get; } =
    [
        Oidc.SubjectTypes.Public
    ];

    public HashSet<string> SupportedDisplayModes { get; } =
    [
        Oidc.DisplayModes.Page,
        Oidc.DisplayModes.Popup,
        Oidc.DisplayModes.Touch,
        Oidc.DisplayModes.Wap
    ];

    public HashSet<string> SupportedPromptModes { get; } =
    [
        Oidc.PromptModes.None,
        Oidc.PromptModes.Login,
        Oidc.PromptModes.Consent,
        Oidc.PromptModes.SelectAccount
    ];

    public HashSet<string> SupportedTokenTypeHints { get; } =
    [
        Oidc.TokenTypeHints.RefreshToken,
        Oidc.TokenTypeHints.AccessToken
    ];

    public bool SubjectTypeIsSupported(string subjectType)
    {
        return SupportedSubjectTypes.Contains(subjectType);
    }

    public bool DisplayModeIsSupported(string displayMode)
    {
        return SupportedDisplayModes.Contains(displayMode);
    }

    public bool PromptModeIsSupported(string promptMode)
    {
        return SupportedPromptModes.Contains(promptMode);
    }

    public bool TokenTypeHintIsSupported(string tokenTypeHint)
    {
        return SupportedTokenTypeHints.Contains(tokenTypeHint);
    }

    private static object? CloneJsonValue(object? value)
    {
        if (value is null)
            return null;

        var element = JsonSerializer.SerializeToElement(value, value.GetType());
        return MaterializeJsonValue(element);
    }

    private static object? MaterializeJsonValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.String => element.GetString(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number => MaterializeNumber(element),
        JsonValueKind.Array => element.EnumerateArray().Select(MaterializeJsonValue).ToList(),
        JsonValueKind.Object => element.EnumerateObject().ToDictionary(
            property => property.Name,
            property => MaterializeJsonValue(property.Value),
            StringComparer.Ordinal),
        _ => throw new JsonException($"Unsupported discovery custom-entry JSON value kind '{element.ValueKind}'."),
    };

    private static object MaterializeNumber(JsonElement element)
    {
        if (element.TryGetInt32(out var int32))
            return int32;

        if (element.TryGetInt64(out var int64))
            return int64;

        if (element.TryGetUInt64(out var uint64))
            return uint64;

        if (element.TryGetDecimal(out var decimalValue))
            return decimalValue;

        return element.GetDouble();
    }
}

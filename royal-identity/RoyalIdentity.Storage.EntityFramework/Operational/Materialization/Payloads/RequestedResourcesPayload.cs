using RoyalIdentity.Models.Scopes;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Materialization.Payloads;

/// <summary>A persisted <see cref="ProtectedResource"/> (RFC 8707 / RFC 9728 metadata).</summary>
public sealed class ProtectedResourcePayload
{
    public required string ResourceUri { get; set; }

    public bool ShowInDiscoveryDocument { get; set; }

    public string? DisplayName { get; set; }

    public string? DocumentationUri { get; set; }

    public string? PolicyUri { get; set; }

    public string? TosUri { get; set; }

    public static ProtectedResourcePayload From(ProtectedResource resource) => new()
    {
        ResourceUri = resource.ResourceUri,
        ShowInDiscoveryDocument = resource.ShowInDiscoveryDocument,
        DisplayName = resource.DisplayName,
        DocumentationUri = resource.DocumentationUri,
        PolicyUri = resource.PolicyUri,
        TosUri = resource.TosUri,
    };

    public ProtectedResource ToProtectedResource() => new(ResourceUri)
    {
        ShowInDiscoveryDocument = ShowInDiscoveryDocument,
        DisplayName = DisplayName,
        DocumentationUri = DocumentationUri,
        PolicyUri = PolicyUri,
        TosUri = TosUri,
    };
}

/// <summary>A persisted <see cref="Scope"/>.</summary>
public sealed class ScopePayload
{
    public ScopeVisibility Visibility { get; set; }

    public required string Name { get; set; }

    public required string DisplayName { get; set; }

    public required string Description { get; set; }

    public bool Enabled { get; set; }

    public bool ShowInDiscoveryDocument { get; set; }

    public bool Required { get; set; }

    public bool Emphasize { get; set; }

    public static ScopePayload From(Scope scope) => new()
    {
        Visibility = scope.Visibility,
        Name = scope.Name,
        DisplayName = scope.DisplayName,
        Description = scope.Description,
        Enabled = scope.Enabled,
        ShowInDiscoveryDocument = scope.ShowInDiscoveryDocument,
        Required = scope.Required,
        Emphasize = scope.Emphasize,
    };

    // ShowInDiscoveryDocument is assigned after construction on purpose: the constructor derives it from the
    // visibility, and materialization must reproduce what was persisted rather than re-derive it.
    public Scope ToScope() => new(Visibility, Name, DisplayName, Description)
    {
        Enabled = Enabled,
        ShowInDiscoveryDocument = ShowInDiscoveryDocument,
        Required = Required,
        Emphasize = Emphasize,
    };
}

/// <summary>A persisted <see cref="IdentityScope"/>.</summary>
public sealed class IdentityScopePayload
{
    public ScopeVisibility Visibility { get; set; }

    public required string Name { get; set; }

    public required string DisplayName { get; set; }

    public required string Description { get; set; }

    public bool Enabled { get; set; }

    public bool ShowInDiscoveryDocument { get; set; }

    public bool Required { get; set; }

    public bool Emphasize { get; set; }

    public required List<string> UserClaims { get; set; }

    public static IdentityScopePayload From(IdentityScope scope) => new()
    {
        Visibility = scope.Visibility,
        Name = scope.Name,
        DisplayName = scope.DisplayName,
        Description = scope.Description,
        Enabled = scope.Enabled,
        ShowInDiscoveryDocument = scope.ShowInDiscoveryDocument,
        Required = scope.Required,
        Emphasize = scope.Emphasize,
        UserClaims = [.. scope.UserClaims],
    };

    public IdentityScope ToIdentityScope(string payloadName)
    {
        if (UserClaims.Count is 0)
        {
            throw OperationalPayloadException.IncompletePayload(
                payloadName, $"the identity scope '{Name}' has no user claims");
        }

        // The IdentityScope constructor derives Description from DisplayName, so the persisted Description is
        // reassigned here; otherwise materialization would silently rewrite it.
        return new IdentityScope(Visibility, Name, DisplayName, Description, UserClaims)
        {
            Description = Description,
            Enabled = Enabled,
            ShowInDiscoveryDocument = ShowInDiscoveryDocument,
            Required = Required,
            Emphasize = Emphasize,
        };
    }
}

/// <summary>
/// A persisted <see cref="ResourceServer"/>. <see cref="ResourceServer.Secrets"/> is deliberately outside the
/// contract: it is resource-server authentication configuration, it has no reader in the code-exchange path,
/// and copying it into every short-lived operational record would spread credentials for no gain. Like the
/// claim metadata of <see cref="ClaimPayload"/>, this is an omission by decision — needing it later requires
/// a new explicit payload version.
/// </summary>
public sealed class ResourceServerPayload
{
    public ScopeVisibility Visibility { get; set; }

    public required string Name { get; set; }

    public required string DisplayName { get; set; }

    public required string Description { get; set; }

    public bool Enabled { get; set; }

    public bool ShowInDiscoveryDocument { get; set; }

    public string? Audience { get; set; }

    public bool AllowScopeRequests { get; set; }

    public required List<ScopePayload> Scopes { get; set; }

    public required List<ProtectedResourcePayload> ProtectedResources { get; set; }

    public required List<string> AllowedAccessTokenSigningAlgorithms { get; set; }

    public static ResourceServerPayload From(ResourceServer server) => new()
    {
        Visibility = server.Visibility,
        Name = server.Name,
        DisplayName = server.DisplayName,
        Description = server.Description,
        Enabled = server.Enabled,
        ShowInDiscoveryDocument = server.ShowInDiscoveryDocument,
        Audience = server.Audience,
        AllowScopeRequests = server.AllowScopeRequests,
        Scopes = [.. server.Scopes.Select(ScopePayload.From)],
        ProtectedResources = [.. server.ProtectedResources.Select(ProtectedResourcePayload.From)],
        AllowedAccessTokenSigningAlgorithms = [.. server.AllowedAccessTokenSigningAlgorithms],
    };

    public ResourceServer ToResourceServer() => new(Visibility, Name, DisplayName, Description)
    {
        Enabled = Enabled,
        ShowInDiscoveryDocument = ShowInDiscoveryDocument,
        Audience = Audience,
        AllowScopeRequests = AllowScopeRequests,
        Scopes = [.. Scopes.Select(scope => scope.ToScope())],
        ProtectedResources = [.. ProtectedResources.Select(resource => resource.ToProtectedResource())],
        AllowedAccessTokenSigningAlgorithms = [.. AllowedAccessTokenSigningAlgorithms],
    };
}

/// <summary>
/// The persisted <see cref="RequestedResources"/> of an authorization code: the resources that were actually
/// resolved for the request, so the token endpoint reproduces the authorization decision instead of
/// re-resolving it against configuration that may have changed since.
/// </summary>
public sealed class RequestedResourcesPayload
{
    public bool OfflineAccess { get; set; }

    public required List<string> RequestedScopeNames { get; set; }

    public required List<string> MissingScopes { get; set; }

    public required List<string> RequestedResourceUris { get; set; }

    public required List<string> InvalidTargets { get; set; }

    public required List<IdentityScopePayload> IdentityScopes { get; set; }

    public required List<ScopePayload> Scopes { get; set; }

    public required List<ResourceServerPayload> ResourceServers { get; set; }

    public required List<ProtectedResourcePayload> ProtectedResources { get; set; }

    public static RequestedResourcesPayload From(RequestedResources resources)
    {
        ArgumentNullException.ThrowIfNull(resources);

        return new RequestedResourcesPayload
        {
            OfflineAccess = resources.OfflineAccess,
            RequestedScopeNames = [.. resources.RequestedScopeNames],
            MissingScopes = [.. resources.MissingScopes],
            RequestedResourceUris = [.. resources.RequestedResourceUris],
            InvalidTargets = [.. resources.InvalidTargets],
            IdentityScopes = [.. resources.IdentityScopes.Select(IdentityScopePayload.From)],
            Scopes = [.. resources.Scopes.Select(ScopePayload.From)],
            ResourceServers = [.. resources.ResourceServers.Select(ResourceServerPayload.From)],
            ProtectedResources = [.. resources.ProtectedResources.Select(ProtectedResourcePayload.From)],
        };
    }

    public RequestedResources ToRequestedResources(string payloadName)
    {
        var resources = new RequestedResources { OfflineAccess = OfflineAccess };

        foreach (var name in RequestedScopeNames)
            resources.RequestedScopeNames.Add(name);

        foreach (var name in MissingScopes)
            resources.MissingScopes.Add(name);

        foreach (var uri in RequestedResourceUris)
            resources.RequestedResourceUris.Add(uri);

        foreach (var target in InvalidTargets)
            resources.InvalidTargets.Add(target);

        foreach (var scope in IdentityScopes)
            resources.IdentityScopes.Add(scope.ToIdentityScope(payloadName));

        foreach (var scope in Scopes)
            resources.Scopes.Add(scope.ToScope());

        foreach (var server in ResourceServers)
            resources.ResourceServers.Add(server.ToResourceServer());

        foreach (var resource in ProtectedResources)
            resources.ProtectedResources.Add(resource.ToProtectedResource());

        return resources;
    }
}

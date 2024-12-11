using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;

namespace RoyalIdentity.Contracts.Defaults;

public class DefaultExtensionsGrantsProvider : IExtensionsGrantsProvider
{
    private readonly ILogger logger;
    private readonly IEnumerable<IExtensionGrant> extensions;
    private readonly IReadOnlyList<string> grants;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultExtensionsGrantsProvider"/> class.
    /// </summary>
    /// <param name="extensions">The validators.</param>
    /// <param name="logger">The logger.</param>
    public DefaultExtensionsGrantsProvider(
        IEnumerable<IExtensionGrant> extensions,
        ILogger<DefaultExtensionsGrantsProvider> logger)
    {
        this.extensions = extensions;
        this.logger = logger;

        grants = extensions.Select(v => v.GrantType).ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetAvailableGrantTypes() => grants;

    /// <inheritdoc />
    public ValueTask<ITokenEndpointContextBase> CreateContextAsync(string grantType, CancellationToken ct)
    {
        var grant = extensions.FirstOrDefault(v => v.GrantType.Equals(grantType, StringComparison.Ordinal));
        if (grant is null)
        {
            logger.LogError("No validator found for grant type");
            throw new InvalidOperationException("No validator found for grant type");
        }

        return grant.CreateContextAsync(ct);
    }
}
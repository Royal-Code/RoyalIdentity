using Microsoft.Extensions.Logging;

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

    /// <summary>
    /// Gets the available grant types.
    /// </summary>
    /// <returns></returns>
    public IReadOnlyList<string> GetAvailableGrantTypes() => grants;

    // /// <summary>
    // /// Validates the request.
    // /// </summary>
    // /// <param name="request">The request.</param>
    // /// <returns></returns>
    // public async Task<GrantValidationResult> ValidateAsync(ValidatedTokenRequest request)
    // {
    //     var validator = extensions.FirstOrDefault(v => v.GrantType.Equals(request.GrantType, StringComparison.Ordinal));
    //
    //     if (validator == null)
    //     {
    //         logger.LogError("No validator found for grant type");
    //         return new GrantValidationResult(TokenRequestErrors.UnsupportedGrantType);
    //     }
    //
    //     try
    //     {
    //         logger.LogTrace("Calling into custom grant validator: {type}", validator.GetType().FullName);
    //
    //         var context = new ExtensionGrantValidationContext
    //         {
    //             Request = request
    //         };
    //
    //         await validator.ValidateAsync(context);
    //         return context.Result;
    //     }
    //     catch (Exception e)
    //     {
    //         logger.LogError(1, e, "Grant validation error: {message}", e.Message);
    //         return new GrantValidationResult(TokenRequestErrors.InvalidGrant);
    //     }
    // }
}
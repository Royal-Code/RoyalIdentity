using RoyalIdentity.Contracts.Models;

namespace RoyalIdentity.Contracts;

/// <summary>
///  Authorize endpoint request validator.
/// </summary>
public interface IAuthorizeRequestValidator
{
    /// <summary>
    /// <para>
    ///     Validates authorize request parameters.
    /// </para>
    /// <para>
    ///     When the parameters are correct, an authorisation context is generated,
    ///     when they are invalid, error details are generated.
    /// </para>
    /// </summary>
    /// <param name="request"></param>
    /// <returns></returns>
    Task<AuthorizationValidationResult> ValidateAsync(AuthorizationValidationRequest request, CancellationToken ct);
}

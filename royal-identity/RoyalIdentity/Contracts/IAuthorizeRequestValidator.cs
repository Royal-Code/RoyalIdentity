using RoyalIdentity.Contexts;

namespace RoyalIdentity.Contracts;

/// <summary>
///  Authorize endpoint request validator.
/// </summary>
public interface IAuthorizeRequestValidator
{
    /// <summary>
    ///  Validates authorize request parameters.
    /// </summary>
    /// <param name="parameters"></param>
    /// <param name="subject"></param>
    /// <returns></returns>
    Task ValidateAsync(AuthorizeValidationContext context, CancellationToken ct);
}

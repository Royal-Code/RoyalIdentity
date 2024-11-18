using RoyalIdentity.Contracts.Models;

namespace RoyalIdentity.Contracts;

/// <summary>
/// Interface for the token validator
/// </summary>
public interface ITokenValidator
{
    /// <summary>
    /// Validates an JWT access token.
    /// </summary>
    /// <param name="jwt"></param>
    /// <param name="expectedScope"></param>
    /// <param name="audience"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<TokenEvaluationResult> ValidateJwtAccessTokenAsync(string jwt, string? expectedScope = null, string? audience = null, CancellationToken ct = default);

    /// <summary>
    /// Validates a reference access token, not jwt.
    /// </summary>
    /// <param name="tokenHandle"></param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<TokenEvaluationResult> ValidateReferenceAccessTokenAsync(string jti, CancellationToken ct = default);

    /// <summary>
    /// Validates an identity token.
    /// </summary>
    /// <param name="token">The token.</param>
    /// <param name="clientId">The client identifier. Optional, if not informed, client will not be validated.</param>
    /// <param name="validateLifetime">if set to <c>true</c> the lifetime gets validated. Otherwise not.</param>
    /// <returns></returns>
    Task<TokenEvaluationResult> ValidateIdentityTokenAsync(string token, string? clientId = null, bool validateLifetime = true);
}

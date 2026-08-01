// This file contains material derived from IdentityServer4 and/or IdentityModel.
// Original component copyrights remain with Brock Allen, Dominick Baier, and/or Duende Software.
// Licensed under Apache License 2.0; see LICENSES/Apache-2.0.txt and THIRD-PARTY-NOTICES.md.
// Modified by RoyalIdentity contributors for the RoyalIdentity rearchitecture.
//
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

﻿using RoyalIdentity.Models;

namespace RoyalIdentity.Contracts.Storage;

/// <summary>
/// Interface for user consent storage
/// </summary>
public interface IUserConsentStore
{
    /// <summary>
    /// Stores the user consent.
    /// </summary>
    /// <param name="consent">The consent.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task StoreUserConsentAsync(Consent consent, CancellationToken ct);

    /// <summary>
    /// Gets the user consent.
    /// </summary>
    /// <param name="subjectId">The subject identifier.</param>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task<Consent?> GetUserConsentAsync(string subjectId, string clientId, CancellationToken ct);

    /// <summary>
    /// Removes the user consent.
    /// </summary>
    /// <param name="subjectId">The subject identifier.</param>
    /// <param name="clientId">The client identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    Task RemoveUserConsentAsync(string subjectId, string clientId, CancellationToken ct);
}

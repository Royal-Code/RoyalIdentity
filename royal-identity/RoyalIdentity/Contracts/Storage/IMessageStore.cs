// This file contains material derived from IdentityServer4 and/or IdentityModel.
// Original component copyrights remain with Brock Allen, Dominick Baier, and/or Duende Software.
// Licensed under Apache License 2.0; see LICENSES/Apache-2.0.txt and THIRD-PARTY-NOTICES.md.
// Modified by RoyalIdentity contributors for the RoyalIdentity rearchitecture.
//
using RoyalIdentity.Contracts.Models.Messages;

namespace RoyalIdentity.Contracts.Storage;

/// <summary>
/// Interface for a message store
/// </summary>
public interface IMessageStore
{
    /// <summary>
    /// Writes the message.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <returns>An identifier for the message</returns>
    ValueTask<string> WriteAsync<TModel>(Message<TModel> message, CancellationToken ct);

    /// <summary>
    /// Reads the message.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <typeparam name="TModel">The type of the model.</typeparam>
    /// <returns></returns>
    ValueTask<Message<TModel>?> ReadAsync<TModel>(string id, CancellationToken ct);

    /// <summary>
    /// Deletes the message, if present.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns></returns>
    ValueTask DeleteAsync(string id, CancellationToken ct);
}

// This file contains material derived from IdentityServer4 and/or IdentityModel.
// Original component copyrights remain with Brock Allen, Dominick Baier, and/or Duende Software.
// Licensed under Apache License 2.0; see LICENSES/Apache-2.0.txt and THIRD-PARTY-NOTICES.md.
// Modified by RoyalIdentity contributors for the RoyalIdentity rearchitecture.
//
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Contracts.Models.Messages;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Utils;
using Base64Url = RoyalIdentity.Security.Encoding.Base64Url;

namespace RoyalIdentity.Contracts.Defaults;

/// <summary>
/// <see cref="IMessageStore"/> implementation that uses data protection to protect message.
/// </summary>
public class ProtectedDataMessageStore : IMessageStore
{
    private const string Purpose = "RoyalIdentity.ProtectedDataMessageStore";

    private readonly IDataProtector protector;
    private readonly ILogger logger;

    /// <summary>
    /// Ctor
    /// </summary>
    /// <param name="provider"></param>
    /// <param name="logger"></param>
    public ProtectedDataMessageStore(IDataProtectionProvider provider, ILogger<ProtectedDataMessageStore> logger)
    {
        protector = provider.CreateProtector(Purpose);
        this.logger = logger;
    }

    /// <inheritdoc />
    public ValueTask<string> WriteAsync<TModel>(Message<TModel> message, CancellationToken ct)
    {
        var json = Json.Serialize(message);
        var bytes = Encoding.UTF8.GetBytes(json);
        bytes = protector.Protect(bytes);
        var id = Base64Url.Encode(bytes);

        return ValueTask.FromResult(id);
    }

    /// <inheritdoc />
    public ValueTask<Message<TModel>?> ReadAsync<TModel>(string id, CancellationToken ct)
    {
        if (id.IsMissing())
            return default;

        Message<TModel>? result = null;

        try
        {
            var bytes = Base64Url.Decode(id);
            bytes = protector.Unprotect(bytes);
            var json = Encoding.UTF8.GetString(bytes);
            result = Json.Deserialize<Message<TModel>>(json);
        }
        catch(Exception ex)
        {
            logger.LogError(ex, "Exception reading protected message");
        }

        return ValueTask.FromResult(result);
    }

    public ValueTask DeleteAsync(string logoutId, CancellationToken ct) => default;
}

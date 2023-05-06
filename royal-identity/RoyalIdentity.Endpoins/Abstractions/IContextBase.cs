
using Microsoft.AspNetCore.Http;

namespace RoyalIdentity.Endpoins.Abstractions;

/// <summary>
/// Base interface for all contexts
/// </summary>
public interface IContextBase
{
    HttpContext HttpContext { get; }

    ContextItems Items { get; }

    IResponseHandler? Response { get; }
}

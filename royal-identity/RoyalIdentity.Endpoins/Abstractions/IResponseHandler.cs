
using Microsoft.AspNetCore.Http;

namespace RoyalIdentity.Endpoins.Abstractions;

public interface IResponseHandler
{
    Task<IResult> CreateResponseAsync();
}

using Microsoft.AspNetCore.Http;

namespace RoyalIdentity.Endpoints.Abstractions;

public interface IResponseHandler
{
    Task<IResult> CreateResponseAsync();
}
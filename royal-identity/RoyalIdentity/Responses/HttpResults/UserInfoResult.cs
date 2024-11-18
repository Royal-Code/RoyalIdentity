using Microsoft.AspNetCore.Http;
using RoyalIdentity.Extensions;

namespace RoyalIdentity.Responses.HttpResults;

public class UserInfoResult : IResult, IStatusCodeHttpResult
{
    private readonly IDictionary<string, object> userData;

    public UserInfoResult(IDictionary<string, object> userData)
    {
        this.userData = userData;
    }

    public int? StatusCode => StatusCodes.Status200OK;

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.SetNoCache();
        await httpContext.Response.WriteJsonAsync(userData);
    }
}

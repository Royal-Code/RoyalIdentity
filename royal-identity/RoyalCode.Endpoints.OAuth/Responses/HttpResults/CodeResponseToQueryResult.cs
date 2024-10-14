using Microsoft.AspNetCore.Http;

namespace RoyalIdentity.Responses.HttpResults
{
    public class CodeResponseToQueryResult : IResult, IStatusCodeHttpResult
    {


        public Task ExecuteAsync(HttpContext httpContext)
        {
            throw new NotImplementedException();
        }

        public int? StatusCode => 200;
    }
}
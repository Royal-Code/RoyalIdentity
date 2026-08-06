using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RoyalIdentity.Pipelines.Defaults;

namespace Tests.Pipelines.Defaults;

public class ResponseHandlerTests
{
    [Fact]
    public void ErrorFactory_MustExposeItsProtocolErrorThroughHasProblem()
    {
        var handler = ResponseHandler.Error(
            "invalid_request",
            "The request is malformed.",
            "https://issuer.example/errors/invalid-request",
            StatusCodes.Status401Unauthorized);

        var hasProblem = handler.HasProblem(out var problem);

        Assert.True(hasProblem);
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status401Unauthorized, problem.Status);
        Assert.Equal("invalid_request", problem.Title);
        Assert.Equal("The request is malformed.", problem.Detail);
        Assert.Equal("https://issuer.example/errors/invalid-request", problem.Instance);
    }

    [Fact]
    public void ProblemDetailsResult_MustRemainVisibleThroughHasProblem()
    {
        var expected = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid input",
            Detail = "The input is invalid."
        };
        var handler = new ResponseHandler(Results.BadRequest(expected));

        var hasProblem = handler.HasProblem(out var problem);

        Assert.True(hasProblem);
        Assert.Same(expected, problem);
    }

    [Fact]
    public void SuccessfulResult_MustNotExposeAProblem()
    {
        var handler = ResponseHandler.Ok();

        var hasProblem = handler.HasProblem(out var problem);

        Assert.False(hasProblem);
        Assert.Null(problem);
    }
}

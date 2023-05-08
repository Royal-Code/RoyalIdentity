using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Tests.Endpoints;

public class ServerEndpointTests
{
    [Fact]
    public async Task EndpointHandler_Must_CreateResponse()
    {
        // arrange
        var httpContext = new DefaultHttpContext();
        var endpointHandler = new TestEndpointHandler();
        var contextPipeline = new TestContextPipeline();

        // act
        var result = await ServerEndpoint<TestEndpointHandler, TestContext>.EndpointHandler(
            httpContext,
            endpointHandler,
            contextPipeline);

        // assert
        Assert.IsType<Ok<TestContext>>(result);
    }
}

file class TestEndpointHandler : IEndpointHandler<TestContext>
{
    public ValueTask<bool> TryCreateContextAsync(HttpContext httpContext, out TestContext context)
    {
        context = new TestContext(httpContext);
        return new ValueTask<bool>(true);
    }
}

file class TestContext : AbstractContextBase
{
    public TestContext(HttpContext httpContext) : base(httpContext) { }
}

file class TestContextPipeline : IContextPipeline<TestContext>
{
    public async Task SendAsync(TestContext context)
    {
        context.Response = new TestResponseHandler(context);
        await Task.CompletedTask;
    }

    private class TestResponseHandler : IResponseHandler
    {
        private TestContext context;

        public TestResponseHandler(TestContext context)
        {
            this.context = context;
        }

        public Task<IResult> CreateResponseAsync()
        {
            return Task.FromResult(Results.Ok(context));
        }
    }
}
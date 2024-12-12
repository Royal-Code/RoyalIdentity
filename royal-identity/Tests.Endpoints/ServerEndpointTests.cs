using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics.CodeAnalysis;

namespace Tests.Endpoints;

public class ServerEndpointTests
{
    [Fact]
    public async Task EndpointHandler_Must_CreateResponse()
    {
        // arrange
        var httpContext = new DefaultHttpContext();
        var endpointHandler = new TestEndpointHandler();
        var pipelineDispatcher = new TestPipelineDispatcher();
        var logger = new Logger<TestEndpointHandler>(new NullLoggerFactory());

        // act
        var result = await ServerEndpoint<TestEndpointHandler>.EndpointHandler(
            httpContext,
            endpointHandler,
            pipelineDispatcher,
            logger);

        // assert
        Assert.IsType<Ok<TestContext>>(result);
    }
}

file class TestPipelineDispatcher : IPipelineDispatcher
{
    public Task SendAsync(IContextBase context, CancellationToken ct)
    {
        var contextPipeline = new TestContextPipeline();
        return contextPipeline.SendAsync((TestContext)context, ct);
    }
}

file class TestEndpointHandler : IEndpointHandler
{
    public ValueTask<bool> TryCreateContextAsync(HttpContext httpContext, out TestContext context)
    {
        context = new TestContext(httpContext);
        return new ValueTask<bool>(true);
    }

    public ValueTask<EndpointCreationResult> TryCreateContextAsync(HttpContext httpContext)
    {
        var context = new TestContext(httpContext);
        return ValueTask.FromResult(new EndpointCreationResult(context));
    }
}

file class TestContext : AbstractContextBase
{
    public TestContext(HttpContext httpContext) : base(httpContext) { }
}

file class TestContextPipeline : IContextPipeline<TestContext>
{
    public async Task SendAsync(TestContext context, CancellationToken ct)
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
        public ValueTask<IResult> CreateResponseAsync(CancellationToken ct)
        {
            return ValueTask.FromResult(Results.Ok(context));
        }

        public bool HasProblem([NotNullWhen(true)] out ProblemDetails? problem)
        {
            problem = null;
            return false;
        }
    }
}
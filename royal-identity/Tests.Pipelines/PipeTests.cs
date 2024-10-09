using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RoyalIdentity.Endpoints.Abstractions;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Pipelines.Infrastructure;

namespace Tests.Pipelines;

public class PipeTests
{
    [Fact]
    public async Task PipelineDispatcher_Must_Dispatch()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddTransient<TestHandler>();
        services.AddPipelines(builder =>
        {
            builder.For<TestContext>()
                .UseHandler<TestHandler>();
        });

        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IPipelineDispatcher>();

        // act
        var context = new TestContext(new DefaultHttpContext());
        await dispatcher.SendAsync(context, default);

        // assert
        Assert.NotNull(context.Response);
    }
}

file class TestContext : AbstractContextBase
{
    public TestContext(HttpContext httpContext) : base(httpContext) { }
}

file class TestHandler : IHandler<TestContext>
{
    public ValueTask Handle(TestContext context, CancellationToken cancellationToken)
    {
        context.Response = new TestResponse();
        return ValueTask.CompletedTask;
    }
}

file class TestResponse : IResponseHandler
{
    public async ValueTask<IResult> CreateResponseAsync(CancellationToken ct)
    {
        return Results.Ok();
    }
}
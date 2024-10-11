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

    [Fact]
    public async Task PipelineDispatcher_Must_DispatchDecorateValidateAndHandle()
    {
        // arrange
        var services = new ServiceCollection();
        services.AddTransient<TestHandler>();
        services.AddTransient<ClientContextDecorator>();
        services.AddTransient<ClientValidator>();
        services.AddPipelines(builder =>
        {
            builder.For<TestContext>()
                .UseDecorator<ClientContextDecorator>()
                .UseValidator<ClientValidator>()
                .UseHandler<TestHandler>();
        });

        var sp = services.BuildServiceProvider();
        var dispatcher = sp.GetRequiredService<IPipelineDispatcher>();

        // act
        var context = new TestContext(new DefaultHttpContext());
        await dispatcher.SendAsync(context, default);

        // assert
        Assert.NotNull(context.Response);
        Assert.NotNull(context.Client);

        var result = await context.Response.CreateResponseAsync(default);
        var statusCodeResult = result as IStatusCodeHttpResult;
        Assert.NotNull(statusCodeResult);
        Assert.Equal(200, statusCodeResult.StatusCode);
    }
}


file class ClientContext
{
    public string? Name { get; set; }
}

file interface IContextWithClient : IContextBase
{
    public ClientContext? Client { get; set; }
}

file class TestContext : AbstractContextBase, IContextWithClient
{
    public TestContext(HttpContext httpContext) : base(httpContext) { }

    public ClientContext? Client { get; set; }
}

file class TestHandler : IHandler<TestContext>
{
    public Task Handle(TestContext context, CancellationToken cancellationToken)
    {
        context.Response = new TestResponse();
        return Task.CompletedTask;
    }
}

file class TestResponse : IResponseHandler
{
    public async ValueTask<IResult> CreateResponseAsync(CancellationToken ct)
    {
        return Results.Ok();
    }
}

file class ClientContextDecorator : IDecorator<IContextWithClient>
{
    public ValueTask Decorate(IContextWithClient context, Func<ValueTask> next, CancellationToken cancellationToken)
    {
        context.Client = new ClientContext()
        {
            Name = "Tests"
        };

        return next();
    }
}

file class ClientValidator : IValidator<IContextWithClient>
{
    public ValueTask Validate(IContextWithClient context, CancellationToken cancellationToken)
    {
        if (context.Client == null)
        {
            context.Response = new BadResponse();
        }

        return default;
    }
}

file class BadResponse : IResponseHandler
{
    public async ValueTask<IResult> CreateResponseAsync(CancellationToken ct)
    {
        return Results.BadRequest("invalid client");
    }
}
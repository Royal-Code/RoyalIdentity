using System.Collections.Specialized;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts;
using RoyalIdentity.Extensions;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Pipelines.Defaults;
using RoyalIdentity.Pipelines.Infrastructure;
using Tests.Integration.Prepare;

namespace Tests.Integration.Endpoints;

/// <summary>
/// Proves that grant types without a built-in context are dispatched exclusively through the extension provider.
/// </summary>
public class ExtensionGrantRoutingTests : IClassFixture<ExtensionGrantRoutingTests.ExtensionGrantAppFactory>
{
    private const string ReachedExtension = "urn:example:extension-grant-reached";
    private readonly ExtensionGrantAppFactory factory;

    public ExtensionGrantRoutingTests(ExtensionGrantAppFactory factory) => this.factory = factory;

    [Theory]
    [InlineData(OpenIdConnectGrantTypes.DeviceCode)]
    [InlineData(OpenIdConnectGrantTypes.TokenExchange)]
    public async Task RegisteredStandardizedExtensionGrant_ReachesTheExtensionProvider(string grantType)
    {
        var response = await PostTokenAsync(grantType);

        var error = await response.AssertErrorAsync(ReachedExtension);
        Assert.Equal(grantType, error.Description);
    }

    [Fact]
    public async Task UnregisteredGrant_RemainsExactlyUnsupportedGrantType()
    {
        var response = await PostTokenAsync("urn:example:phase-three-unregistered");

        await response.AssertErrorAsync(Oidc.Token.Errors.UnsupportedGrantType);
    }

    private Task<HttpResponseMessage> PostTokenAsync(string grantType)
    {
        return factory.CreateClient().PostAsync(
            Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                [Oidc.Token.Request.GrantType] = grantType,
                [Oidc.Token.Request.ClientId] = "demo_client",
            }));
    }

    public sealed class ExtensionGrantAppFactory : PersistentStorageAppFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureServices(services =>
            {
                services.AddTransient<IExtensionGrant>(provider => new RoutingExtensionGrant(
                    OpenIdConnectGrantTypes.DeviceCode,
                    provider.GetRequiredService<IHttpContextAccessor>()));
                services.AddTransient<IExtensionGrant>(provider => new RoutingExtensionGrant(
                    OpenIdConnectGrantTypes.TokenExchange,
                    provider.GetRequiredService<IHttpContextAccessor>()));
                services.AddTransient<RoutingExtensionGrantHandler>();
                services.AddPipelines(pipelines => pipelines
                    .For<RoutingExtensionGrantContext>()
                    .UseHandler<RoutingExtensionGrantHandler>());
            });
        }
    }

    private sealed class RoutingExtensionGrant(
        string grantType,
        IHttpContextAccessor httpContextAccessor) : IExtensionGrant
    {
        public string GrantType => grantType;

        public ValueTask<ITokenEndpointContextBase> CreateContextAsync(CancellationToken ct)
        {
            var httpContext = httpContextAccessor.HttpContext!;

            return ValueTask.FromResult<ITokenEndpointContextBase>(new RoutingExtensionGrantContext(
                httpContext,
                httpContext.Request.Form.AsNameValueCollection(),
                grantType));
        }
    }

    private sealed class RoutingExtensionGrantContext(
        HttpContext httpContext,
        NameValueCollection raw,
        string grantType) : TokenEndpointContextBase(httpContext, raw, grantType)
    {
        public override void Load(ILogger logger) => LoadBase(logger);

        public override ClaimsPrincipal? GetSubject() => null;
    }

    private sealed class RoutingExtensionGrantHandler : IHandler<RoutingExtensionGrantContext>
    {
        public Task Handle(RoutingExtensionGrantContext context, CancellationToken ct)
        {
            context.Response = ResponseHandler.Error(
                ReachedExtension,
                context.GrantType,
                statusCode: (int)HttpStatusCode.BadRequest);

            return Task.CompletedTask;
        }
    }
}

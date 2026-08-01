using System.Collections.Specialized;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Contexts;
using RoyalIdentity.Contracts;
using RoyalIdentity.Extensions;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Pipelines.Defaults;
using RoyalIdentity.Pipelines.Infrastructure;
using Tests.Integration.Prepare;

namespace Tests.Integration.Endpoints;

/// <summary>
/// Proves the error contract was not closed around the codes this plan enumerates (DF3).
/// </summary>
/// <remarks>
/// <para>
/// RFC 6749 §8.5 lets extensions define their own error codes, and the project supports extension grants. A
/// design that reached for an enum — the obvious way to make the six base codes exhaustive — would have made
/// both impossible, and <c>invalid_target</c> from RFC 8707 along with them.
/// </para>
/// <para>
/// The grant here runs entirely outside <c>RoyalIdentity</c>, which is the point: it uses only the public
/// writer, exactly as a real extension living in another assembly would, and cannot reach the internal
/// <c>context.Error</c> helper.
/// </para>
/// </remarks>
public class ExtensionGrantErrorTests : IClassFixture<ExtensionGrantErrorTests.ExtensionGrantAppFactory>
{
    /// <summary>A code no RFC defines and no constant in this repository declares.</summary>
    private const string CustomError = "urn:example:teapot_required";

    private const string GrantType = "urn:example:test-grant";

    private readonly ExtensionGrantAppFactory factory;

    public ExtensionGrantErrorTests(ExtensionGrantAppFactory factory) => this.factory = factory;

    [Fact]
    public async Task AnExtensionGrant_CanAnswerWithItsOwnErrorCode()
    {
        var response = await factory.CreateClient().PostAsync(
            Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = GrantType,
                ["client_id"] = "demo_client",
            }));

        var error = await response.AssertErrorAsync(CustomError);

        Assert.Equal("The extension grant refused the request", error.Description);
    }

    [Fact]
    public async Task AnUnregisteredGrantType_IsStillUnsupported()
    {
        // The control: the extension is reachable because it was registered, not because the endpoint stopped
        // checking. Without this, a dispatch bug that accepted everything would look like a passing test above.
        var response = await factory.CreateClient().PostAsync(
            Oidc.Routes.BuildTokenUrl(factory.Handles.Demo.Path),
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "urn:example:not-registered",
                ["client_id"] = "demo_client",
            }));

        await response.AssertErrorAsync(Oidc.Token.Errors.UnsupportedGrantType);
    }

    public class ExtensionGrantAppFactory : PersistentStorageAppFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureServices(services =>
            {
                services.AddTransient<IExtensionGrant, TestExtensionGrant>();

                // The pipeline builder wires the chain; the steps themselves are ordinary services, exactly as
                // AddOpenIdConnectProviderServices registers the built-in ones.
                services.AddTransient<TestExtensionGrantHandler>();
                services.AddPipelines(pipelines => pipelines
                    .For<TestExtensionGrantContext>()
                    .UseHandler<TestExtensionGrantHandler>());
            });
        }
    }

    private sealed class TestExtensionGrant(IHttpContextAccessor httpContextAccessor) : IExtensionGrant
    {
        public string GrantType => ExtensionGrantErrorTests.GrantType;

        public ValueTask<ITokenEndpointContextBase> CreateContextAsync(CancellationToken ct)
        {
            var httpContext = httpContextAccessor.HttpContext!;

            return ValueTask.FromResult<ITokenEndpointContextBase>(
                new TestExtensionGrantContext(httpContext, httpContext.Request.Form.AsNameValueCollection()));
        }
    }

    private sealed class TestExtensionGrantContext : TokenEndpointContextBase
    {
        public TestExtensionGrantContext(HttpContext httpContext, NameValueCollection raw)
            : base(httpContext, raw, ExtensionGrantErrorTests.GrantType)
        {
        }

        public override void Load(ILogger logger) => LoadBase(logger);

        public override ClaimsPrincipal? GetSubject() => null;
    }

    private sealed class TestExtensionGrantHandler : IHandler<TestExtensionGrantContext>
    {
        public Task Handle(TestExtensionGrantContext context, CancellationToken ct)
        {
            // Only the public writer, because that is all an extension in another assembly can reach.
            context.Response = ResponseHandler.Error(
                CustomError,
                "The extension grant refused the request",
                statusCode: (int)HttpStatusCode.BadRequest);

            return Task.CompletedTask;
        }
    }
}

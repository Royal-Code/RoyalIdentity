using Microsoft.Extensions.Logging;

namespace Aspire.Tests;

/// <summary>
/// Opt-in acceptance of the local environment: PostgreSQL comes up, the provisioning runner applies the three
/// families and the product seed, and only then does the Server start and answer as a configured realm.
/// <para>
/// It is opt-in because it starts a real container and this project belongs to <c>RoyalIdentity.sln</c>; a
/// solution-wide <c>dotnet test</c> must not require a container runtime. This mirrors the PostgreSQL suites in
/// <c>Tests.Storage</c>.
/// </para>
/// </summary>
public class RoyalIdentityEnvironmentTests
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(5);

    [AspireEnvironmentFact]
    public async Task Environment_ProvisionsTheThreeDatabases_ThenServesTheSeededServerRealm()
    {
        var cancellationToken = new CancellationTokenSource(StartupTimeout).Token;

        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.Aspire_AppHost>(cancellationToken);
        appHost.Services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Debug);
            logging.AddFilter(appHost.Environment.ApplicationName, LogLevel.Debug);
            logging.AddFilter("Aspire.", LogLevel.Debug);
        });
        appHost.Services.ConfigureHttpClientDefaults(clientBuilder =>
            clientBuilder.AddStandardResilienceHandler());

        await using var app = await appHost.BuildAsync(cancellationToken);
        await app.StartAsync(cancellationToken);

        // The runner is a job: it must reach Finished before the Server is allowed to start.
        await app.ResourceNotifications.WaitForResourceAsync(
            "storage-migrations", KnownResourceStates.Finished, cancellationToken);
        await app.ResourceNotifications.WaitForResourceHealthyAsync("royalidentity", cancellationToken);

        var httpClient = app.CreateHttpClient("royalidentity");
        var discovery = await httpClient.GetAsync(
            "/server/.well-known/openid-configuration", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, discovery.StatusCode);
    }
}

/// <summary>Runs only when the environment acceptance is explicitly requested.</summary>
public sealed class AspireEnvironmentFactAttribute : FactAttribute
{
    /// <summary>Environment variable that enables this suite.</summary>
    public const string EnabledVariable = "ROYALIDENTITY_ASPIRE_TESTS";

    public AspireEnvironmentFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnabledVariable)))
        {
            Skip = $"Set {EnabledVariable}=1 and start a container runtime to run the Aspire environment " +
                "acceptance.";
        }
    }
}

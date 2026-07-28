using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Storage.InMemory.Extensions;

#pragma warning disable S1118 // Utility classes should not have public constructors

namespace Tests.Integration.Prepare;

/// <summary>
/// Provider-neutral test-host base. It owns only cross-cutting host concerns; each concrete factory must select
/// its storage and account implementations explicitly.
/// </summary>
public class AppFactoryBase : WebApplicationFactory<Program>
{
    private bool disposed;

    protected AppFactoryBase()
    {
        KeyRingPath = Path.Combine(
            Path.GetTempPath(),
            $"royalidentity-tests-keys-{Guid.NewGuid():N}");
        Directory.CreateDirectory(KeyRingPath);
    }

    protected string KeyRingPath { get; }

    protected const string DataProtectionApplicationName = "RoyalIdentity.Tests";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddDataProtection()
                .PersistKeysToFileSystem(new DirectoryInfo(KeyRingPath))
                .SetApplicationName(DataProtectionApplicationName);
        });

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.AddDebug();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing || disposed)
            return;

        disposed = true;
        if (Directory.Exists(KeyRingPath))
            Directory.Delete(KeyRingPath, recursive: true);
    }
}

/// <summary>
/// Transitional legacy factory. Tests not migrated in Fases 5-6 select the in-memory fake here rather than
/// obtaining it implicitly from <c>Tests.Host</c>.
/// </summary>
public class AppFactory : AppFactoryBase
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services => services.AddInMemoryStorage());
    }
}

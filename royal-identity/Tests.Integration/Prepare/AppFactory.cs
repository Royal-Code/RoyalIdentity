using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

#pragma warning disable S1118 // Utility classes should not have public constructors

namespace Tests.Integration.Prepare;

public class AppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var keyDirectory = new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "data-protection-keys"));
            keyDirectory.Create();

            services.AddDataProtection()
                .PersistKeysToFileSystem(keyDirectory)
                .SetApplicationName("RoyalIdentity.Tests");
        });

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.AddDebug();
        });
    }
}

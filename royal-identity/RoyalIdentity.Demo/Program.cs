using RoyalIdentity.Demo.Components;
using RoyalIdentity.Extensions;
using RoyalIdentity.Razor.Components.Layout;

namespace RoyalIdentity.Demo;

/// <summary>Self-contained, ephemeral SQLite demo entry point.</summary>
public class DemoProgram
{
    public static async Task Main(string[] args)
    {
        var app = BuildApplication(args);
        await app.RunAsync();
    }

    public static WebApplication BuildApplication(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddRoyalIdentityDemo();

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
            app.UseExceptionHandler("/Error", createScopeForErrors: true);

        app.UseStaticFiles();
        app.UseRouting();
        app.UseRoyalIdentityProtocol();
        app.UseAntiforgery();

        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode()
            .AddAdditionalAssemblies(typeof(AccountLayout).Assembly);

        return app;
    }
}

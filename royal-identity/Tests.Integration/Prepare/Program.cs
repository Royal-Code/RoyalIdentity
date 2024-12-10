using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RoyalIdentity.Extensions;
using RoyalIdentity.Storage.InMemory.Extensions;

namespace Tests.Integration.Prepare;

internal class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddHostServices();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
        }

        app.UseStaticFiles();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapOpenIdConnectProviderEndpoints();

        await app.RunAsync();
    }
}

public static class HostServices
{
    public static void AddHostServices(this IServiceCollection services)
    {
        // TODO: Requer configurar com "ServerOptions.Authentication"
        // authentication
        services.AddAuthentication()
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.Events.OnValidatePrincipal = async context =>
                {
                    if (context.Principal is null)
                        return;

                    var isSessionActive = await context.HttpContext.ValidateUserSessionAsync(context.Principal);
                    if (!isSessionActive)
                    {
                        context.RejectPrincipal();
                    }
                };
            });
        services.AddAuthorization();

        // Storage Services
        services.AddInMemoryStorage();

        // RoyalIdentity Services
        services.AddOpenIdConnectProviderServices();
    }
}
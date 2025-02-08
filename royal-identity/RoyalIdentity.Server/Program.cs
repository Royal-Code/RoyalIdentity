using RoyalIdentity.Extensions;
using RoyalIdentity.Razor.Components.Layout;
using RoyalIdentity.Server;
using RoyalIdentity.Server.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddEndpointsApiExplorer();

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

app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(AccountLayout).Assembly);

await app.RunAsync();

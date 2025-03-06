using RoyalIdentity.Authentication;
using RoyalIdentity.Extensions;
using RoyalIdentity.Razor.Components.Layout;
using Tests.Host;
using Tests.Host.Components;

#pragma warning disable S1118 // public Program 
#pragma warning disable S6966 // RunAsync() is not required

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHostServices();

var app = builder.Build();

app.UseStaticFiles();
app.UseExceptionHandler("/Exception", createScopeForErrors: true);

app.UseRealmDiscovery();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapOpenIdConnectProviderEndpoints();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(AccountLayout).Assembly);

app.MapTestHostEndpoints();

app.Run();

public partial class Program
{
}
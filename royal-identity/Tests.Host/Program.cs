using RoyalIdentity.Extensions;
using Tests.Host;

#pragma warning disable S1118 // public Program 
#pragma warning disable S6966 // RunAsync() is not required

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHostServices();

var app = builder.Build();

app.UseExceptionHandler("/Error", createScopeForErrors: true);
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenIdConnectProviderEndpoints();

app.Run();


public partial class Program { }
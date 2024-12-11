using RoyalIdentity.Extensions;
using RoyalIdentity.Users;
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

app.MapPost("login", async (HttpContext context, ISignInManager signInManager) =>
{
    var username = context.Request.Form["username"].FirstOrDefault() ?? string.Empty;
    var password = context.Request.Form["password"].FirstOrDefault() ?? string.Empty;

    var result = await signInManager.ValidateCredentialsAsync(username, password, context.RequestAborted);

    if (result.Success)
    {
        await signInManager.SignInAsync(result.User, result.Session, false, "pwd", context.RequestAborted);
        return Results.Ok();
    }
    else
    {
        return Results.Problem(
            statusCode: 400, 
            type: "invalid-credentials", 
            title: "Invalid credentials",
            detail: result.ErrorMessage);
    }
});

app.Run();


public partial class Program { }
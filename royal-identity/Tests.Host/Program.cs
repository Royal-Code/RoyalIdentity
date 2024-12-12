using RoyalIdentity.Contracts.Models.Messages;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Extensions;
using RoyalIdentity.Users;
using RoyalIdentity.Users.Contracts;
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

app.MapPost("account/login", async (HttpContext context, ISignInManager signInManager) =>
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

app.MapGet("account/profile", async (HttpContext context, IUserStore userManager) =>
{
    var user = await userManager.GetUserAsync(context.User.GetSubjectId(), context.RequestAborted);
    return Results.Ok(user);
}).RequireAuthorization();

app.MapGet("account/logout", async (HttpContext context, IMessageStore messageStore, ISignOutManager signOutManager) =>
{
    var logoutId = context.Request.Query["logoutId"].FirstOrDefault()
        ?? await signOutManager.CreateLogoutIdAsync(context.RequestAborted);

    if (logoutId is null)
    {
        return Results.Problem(
            statusCode: 400,
            type: "invalid-logout-id",
            title: "Invalid logout id",
            detail: "The logout id is invalid or expired.");
    }

    var message = await messageStore.ReadAsync<LogoutMessage>(logoutId, default);
    var model = message?.Data;

    if (model is null)
    {
        return Results.Problem(
            statusCode: 400,
            type: "invalid-logout-id",
            title: "Invalid logout id",
            detail: "The logout id is invalid or expired.");
    }

    await messageStore.DeleteAsync(logoutId, default);
    _ = await signOutManager.SignOutAsync(model, default);

    return Results.Ok();
});

app.Run();


public partial class Program { }
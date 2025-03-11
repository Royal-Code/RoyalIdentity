// Ignore Spelling: app username

using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Contracts;
using RoyalIdentity.Users;
using RoyalIdentity.Extensions;
using RoyalIdentity.Contracts.Models.Messages;
using RoyalIdentity.Contracts.Models;
using static RoyalIdentity.Options.Constants;
using RoyalIdentity.Models.Tokens;

namespace Tests.Host;

public static class HostEndpoints
{
    public static void MapTestHostEndpoints(this WebApplication app)
    {
        app.MapPost("{realm}/test/account/login", async (HttpContext context,
            ISignInManager signInManager, 
            string realm) =>
        {
            var username = context.Request.Form["username"].FirstOrDefault() ?? string.Empty;
            var password = context.Request.Form["password"].FirstOrDefault() ?? string.Empty;

            var currentRealm = context.GetCurrentRealm();

            var result = await signInManager.AuthenticateUserAsync(currentRealm, username, password, context.RequestAborted);

            if (result.Success)
            {
                await signInManager.SignInAsync(result.User, result.Session, false, context.RequestAborted);
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

        app.MapGet("{realm}/test/account/profile", async (HttpContext context, 
            IStorage storage, 
            string realm) =>
        {
            var currentRealm = context.GetCurrentRealm();
            var users = storage.GetUserStore(currentRealm);
            var user = await users.GetUserAsync(context.User.GetSubjectId(), context.RequestAborted);
            return Results.Ok(user);
        }).RequireAuthorization();

        app.MapGet("{realm}/test/account/logout", async (HttpContext context,
            IMessageStore messageStore,
            ISignOutManager signOutManager,
            string realm) =>
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

        app.MapGet("{realm}/test/account/token", async (HttpContext context,
            IStorage storage,
            ITokenFactory tokenFactory,
            string realm) =>
        {
            var currentRealm = context.GetCurrentRealm();

            var user = context.User;
            if (user.Identity?.IsAuthenticated != true)
            {
                return Results.Unauthorized();
            }

            var clientId = context.Request.Query["client_id"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(clientId))
            {
                return Results.BadRequest("client_id is required");
            }

            var clients = storage.GetClientStore(currentRealm);
            var client = await clients.FindEnabledClientByIdAsync(clientId, context.RequestAborted);
            if (client is null)
            {
                return Results.BadRequest("client_id is invalid");
            }

            var scope = context.Request.Query["scope"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(scope))
            {
                return Results.BadRequest("scope is required");
            }

            var resources = storage.GetResourceStore(currentRealm);
            var requestedResources = await resources.FindResourcesByScopeAsync(scope.Split(' '), true, context.RequestAborted);
            if (requestedResources is null)
            {
                return Results.BadRequest("scope is invalid");
            }

            var accessTokenRequest = new AccessTokenRequest
            {
                HttpContext = context,
                User = user,
                Resources = requestedResources,
                Client = client,
                IdentityType = IdentityProfileTypes.User
            };

            var token = await tokenFactory.CreateAccessTokenAsync(accessTokenRequest, context.RequestAborted);

            var refreshToken = default(RefreshToken);
            if (requestedResources.OfflineAccess)
            {
                var refreshTokenRequest = new RefreshTokenRequest
                {
                    HttpContext = context,
                    Subject = user,
                    Client = client,
                    AccessToken = token
                };

                refreshToken = await tokenFactory.CreateRefreshTokenAsync(refreshTokenRequest, context.RequestAborted);
            }

            var idToken = default(IdentityToken);
            if (requestedResources.IsOpenId)
            {
                var idTokenRequest = new IdentityTokenRequest
                {
                    HttpContext = context,
                    User = user,
                    Client = client,
                    Resources = requestedResources,
                    AccessTokenToHash = token.Token
                };

                idToken = await tokenFactory.CreateIdentityTokenAsync(idTokenRequest, context.RequestAborted);
            }

            return Results.Ok(new
            {
                access_token = token.Token,
                token_type = "Bearer",
                expires_in = token.Lifetime,
                refresh_token = refreshToken?.Token,
                scope,
                id_token = idToken?.Token,
            });
        });

        app.MapGet("{realm}/test/protected-resource", async (HttpContext context) =>
        {
            await Task.Delay(10, context.RequestAborted);

            var people = new List<dynamic>()
            {
                new { Id = 1, Name = "Jo" },
                new { Id = 2, Name = "Bob" },
                new { Id = 3, Name = "Alice" },
                new { Id = 4, Name = "Eve" },
                new { Id = 5, Name = "Mallory" },
                new { Id = 6, Name = "Charlie" }
            };

            context.Response.Headers.CacheControl = "no-store";

            return Results.Ok(people);

        }).RequireAuthorization();

        app.MapGet("callback", async (HttpContext context) => 
        {
            await Task.Delay(10, context.RequestAborted);

            // oauth2 code callback

            // for test purposes, we'll just return the query string parameters as json dictionary

            var dict = context.Request.Query.ToDictionary(x => x.Key, x => x.Value.ToString());

            return Results.Ok(dict);
        });
    }
}

using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Tests.Browser;

/// <summary>
/// Minimal RP used only by the opt-in browser suite. It deliberately implements one bounded check per page;
/// a <c>changed</c> response is observable but never starts an automatic retry loop.
/// </summary>
internal sealed class BrowserRpHost : IAsyncDisposable
{
    private readonly WebApplication application;

    private BrowserRpHost(WebApplication application, Uri origin)
    {
        this.application = application;
        Origin = origin;
    }

    public Uri Origin { get; }

    public static async Task<BrowserRpHost> StartAsync(X509Certificate2 certificate)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(BrowserRpHost).Assembly.FullName,
            EnvironmentName = Environments.Development,
        });
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(options => options.Listen(
            IPAddress.Loopback,
            0,
            listen => listen.UseHttps(certificate)));

        var app = builder.Build();
        app.MapGet("/callback", () => Results.Content(
            "<!doctype html><html><body data-callback=\"true\">callback</body></html>",
            "text/html"));
        app.MapGet("/login", (HttpContext context) => Results.Content(
            CreateLoginPage(context.Request.Query),
            "text/html"));
        app.MapGet("/attacker", (HttpContext context) => Results.Content(
            CreateSiblingPage(context.Request.Query["op_origin"].ToString()),
            "text/html"));
        app.MapGet("/check", (HttpContext context) => Results.Content(
            CreateCheckPage(context.Request.Query),
            "text/html"));

        await app.StartAsync();
        var addresses = app.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()?.Addresses
            ?? throw new InvalidOperationException("The RP did not publish a Kestrel address.");
        var address = Assert.Single(addresses, value => value.StartsWith("https://", StringComparison.Ordinal));
        return new BrowserRpHost(app, new Uri(address));
    }

    public string BuildLoginUrl(Uri opOrigin, string realm, string username, string password)
        => QueryHelpers.AddQueryString(
            new Uri(Origin, "/login").ToString(),
            new Dictionary<string, string?>
            {
                ["op_origin"] = opOrigin.GetLeftPart(UriPartial.Authority),
                ["realm"] = realm,
                ["username"] = username,
                ["password"] = password,
            });

    public string BuildCheckUrl(
        Uri opOrigin,
        string realm,
        string clientId,
        string sessionState,
        bool auto = true)
        => QueryHelpers.AddQueryString(
            new Uri(Origin, "/check").ToString(),
            new Dictionary<string, string?>
            {
                ["op_origin"] = opOrigin.GetLeftPart(UriPartial.Authority),
                ["iframe"] = new Uri(opOrigin, $"/{realm}/connect/checksession").ToString(),
                ["client_id"] = clientId,
                ["session_state"] = sessionState,
                ["auto"] = auto ? "true" : "false",
            });

    public async ValueTask DisposeAsync()
    {
        await application.StopAsync();
        await application.DisposeAsync();
    }

    private static string CreateLoginPage(IQueryCollection query)
    {
        var opOrigin = Require(query, "op_origin").TrimEnd('/');
        var realm = Require(query, "realm");
        var action = HtmlEncoder.Default.Encode($"{opOrigin}/{Uri.EscapeDataString(realm)}/test/account/login");
        var username = HtmlEncoder.Default.Encode(Require(query, "username"));
        var password = HtmlEncoder.Default.Encode(Require(query, "password"));

        return $$"""
            <!doctype html>
            <html>
            <body>
              <form method="post" action="{{action}}">
                <input type="hidden" name="username" value="{{username}}">
                <input type="hidden" name="password" value="{{password}}">
              </form>
              <script>document.forms[0].submit();</script>
            </body>
            </html>
            """;
    }

    private static string CreateSiblingPage(string opOrigin)
    {
        var config = JsonSerializer.Serialize(new { opOrigin });
        return $$"""
            <!doctype html>
            <html>
            <body>
              <script>
                const config = {{config}};
                window.addEventListener('message', event => {
                  if (event.origin !== window.location.origin || typeof event.data !== 'string') return;
                  parent.frames.op.postMessage(event.data, config.opOrigin);
                });
              </script>
            </body>
            </html>
            """;
    }

    private static string CreateCheckPage(IQueryCollection query)
    {
        var opOrigin = Require(query, "op_origin");
        var iframe = HtmlEncoder.Default.Encode(Require(query, "iframe"));
        var clientId = Require(query, "client_id");
        var sessionState = Require(query, "session_state");
        var auto = string.Equals(query["auto"], "true", StringComparison.Ordinal);
        var attacker = HtmlEncoder.Default.Encode(QueryHelpers.AddQueryString(
            "/attacker",
            "op_origin",
            opOrigin));
        var config = JsonSerializer.Serialize(new
        {
            opOrigin,
            clientId,
            sessionState,
            auto,
        });

        return $$"""
            <!doctype html>
            <html>
            <body data-ready="false">
              <output id="status">pending</output>
              <iframe id="op" name="op" hidden src="{{iframe}}"></iframe>
              <iframe id="attacker" name="attacker" hidden src="{{attacker}}"></iframe>
              <script>
                const config = {{config}};
                const opFrame = document.getElementById('op');
                const attackerFrame = document.getElementById('attacker');
                const status = document.getElementById('status');
                let opLoaded = false;
                let attackerLoaded = false;
                let responseCount = 0;

                function markReady() {
                  if (!opLoaded || !attackerLoaded) return;
                  document.body.dataset.ready = 'true';
                  if (config.auto) send(config.clientId, config.sessionState);
                }

                function send(clientId, sessionState) {
                  opFrame.contentWindow.postMessage(`${clientId} ${sessionState}`, config.opOrigin);
                }

                opFrame.addEventListener('load', () => { opLoaded = true; markReady(); });
                attackerFrame.addEventListener('load', () => { attackerLoaded = true; markReady(); });
                window.addEventListener('message', event => {
                  if (event.source !== opFrame.contentWindow || event.origin !== config.opOrigin) return;
                  responseCount++;
                  status.textContent = String(event.data);
                });

                window.__checkSession = {
                  targetOrigin: config.opOrigin,
                  send: (clientId, sessionState) => send(clientId, sessionState),
                  sendMalformed: () => opFrame.contentWindow.postMessage('malformed', config.opOrigin),
                  sendFromSibling: () => attackerFrame.contentWindow.postMessage(
                    `${config.clientId} ${config.sessionState}`,
                    window.location.origin),
                  snapshot: () => ({ status: status.textContent, responseCount })
                };
              </script>
            </body>
            </html>
            """;
    }

    private static string Require(IQueryCollection query, string name)
    {
        var value = query[name].ToString();
        return string.IsNullOrEmpty(value)
            ? throw new BadHttpRequestException($"Query parameter '{name}' is required.")
            : value;
    }
}

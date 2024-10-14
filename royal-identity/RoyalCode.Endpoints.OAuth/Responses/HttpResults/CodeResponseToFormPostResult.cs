using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RoyalIdentity.Extensions;
using RoyalIdentity.Options;
using System.Collections.Specialized;
using System.Text.Encodings.Web;

namespace RoyalIdentity.Responses.HttpResults;

public class CodeResponseToFormPostResult : IResult, IStatusCodeHttpResult
{
    private readonly string redirectUri;
    private readonly NameValueCollection parameters;

    public CodeResponseToFormPostResult(string redirectUri, NameValueCollection parameters)
    {
        this.redirectUri = redirectUri;
        this.parameters = parameters;
    }

    public int? StatusCode => 200;

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.SetNoCache();
        AddSecurityHeaders(httpContext);
        await httpContext.Response.WriteHtmlAsync(GetFormPostHtml());
    }

    private const string FormPostHtml = "<html><head><meta http-equiv='X-UA-Compatible' content='IE=edge' /><base target='_self'/></head><body><form method='post' action='{uri}'>{body}<noscript><button>Click to continue</button></noscript></form><script>window.addEventListener('load', function(){document.forms[0].submit();});</script></body></html>";

    private string GetFormPostHtml()
    {
        var html = FormPostHtml;

        var url = HtmlEncoder.Default.Encode(redirectUri);
        html = html.Replace("{uri}", url);
        html = html.Replace("{body}", parameters.ToFormPost());

        return html;
    }

    private static void AddSecurityHeaders(HttpContext context)
    {
        var options = context.RequestServices.GetRequiredService<IOptions<ServerOptions>>().Value.Csp;

        context.Response.AddScriptCspHeaders(options, "sha256-orD0/VhH8hLqrLxKHD/HUEMdwqX6/0ve7c5hspX5VJ8=");

        var referrer_policy = "no-referrer";
        if (!context.Response.Headers.ContainsKey("Referrer-Policy"))
        {
            context.Response.Headers.Append("Referrer-Policy", referrer_policy);
        }
    }
}

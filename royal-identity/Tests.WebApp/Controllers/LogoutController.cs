using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;

namespace Tests.WebApp.Controllers;

public class LogoutController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        // Redireciona para o servidor OIDC para logout
        return SignOut(
            new AuthenticationProperties
            {
                RedirectUri = "/Home" // URL de retorno após o logout
            },
            CookieAuthenticationDefaults.AuthenticationScheme,
            OpenIdConnectDefaults.AuthenticationScheme);
    }
}
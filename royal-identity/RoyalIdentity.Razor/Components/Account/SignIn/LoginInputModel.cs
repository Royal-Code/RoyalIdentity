using System.ComponentModel.DataAnnotations;

namespace RoyalIdentity.Razor.Components.Account.SignIn;

public class LoginInputModel
{
    [Required]
    public string? Username { get; set; }

    [Required]
    public string? Password { get; set; }

    public bool RememberLogin { get; set; }

    [Required]
    public string? ReturnUrl { get; set; }
}
using System.ComponentModel.DataAnnotations;
using RoyalIdentity.Razor.Localization;

namespace RoyalIdentity.Razor.ViewModels;

/// <summary>
/// Local login form. The attributes carry catalogue keys rather than English sentences; the SSR summary
/// resolves them in the request's culture (plan-localization DF18).
/// </summary>
public class LoginInputModel
{
    [Required(ErrorMessage = ValidationMessages.Required)]
    [Display(Name = ValidationMessages.FieldUsername)]
    public string? Username { get; set; }

    [Required(ErrorMessage = ValidationMessages.Required)]
    [Display(Name = ValidationMessages.FieldPassword)]
    public string? Password { get; set; }

    public bool RememberLogin { get; set; }

    [Required(ErrorMessage = ValidationMessages.Required)]
    [Display(Name = ValidationMessages.FieldReturnUrl)]
    public string? ReturnUrl { get; set; }
}

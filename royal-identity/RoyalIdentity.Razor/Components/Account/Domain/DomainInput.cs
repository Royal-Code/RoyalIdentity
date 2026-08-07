using System.ComponentModel.DataAnnotations;
using RoyalIdentity.Razor.Localization;

namespace RoyalIdentity.Razor.Components.Account.Domain;

/// <summary>
/// Domain selection form. Like the other input models, the attributes carry catalogue keys and the localized
/// validation components resolve them in the request's culture (plan-localization DF18).
/// </summary>
public class DomainInput
{
    [Required(ErrorMessage = ValidationMessages.Required)]
    [Display(Name = ValidationMessages.FieldDomain)]
    public string? Domain { get; set; }
}

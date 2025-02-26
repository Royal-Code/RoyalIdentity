// Ignore Spelling: Username

namespace RoyalIdentity.Options;

public class LoginOptions
{
    public bool Enabled { get; set; }

    public bool AllowRegistration { get; set; }
    
    public bool AllowRememberMe { get; set; }

    public bool AllowForgotPassword { get; set; }

    public bool AllowChangePassword { get; set; }

    public bool AllowUpdateProfile { get; set; }

    public bool AllowChangeEmail { get; set; }

    public bool AllowChangeUsername { get; set; }

    public bool AllowChangePhoneNumber { get; set; }

    public bool AllowDeleteAccount { get; set; }

    public bool AllowTwoFactorAuthentication { get; set; }

    public bool AllowSocialLogin { get; set; }

    public bool EmailAsUsername { get; set; }

    public bool LoginWithEmail { get; set; }

    public bool AllowDuplicateEmail { get; set; }

    public bool VerifyEmail { get; set; }
}
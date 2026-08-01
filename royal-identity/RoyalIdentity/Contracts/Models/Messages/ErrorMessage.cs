// This file contains material derived from IdentityServer4 and/or IdentityModel.
// Original component copyrights remain with Brock Allen, Dominick Baier, and/or Duende Software.
// Licensed under Apache License 2.0; see LICENSES/Apache-2.0.txt and THIRD-PARTY-NOTICES.md.
// Modified by RoyalIdentity contributors for the RoyalIdentity rearchitecture.
//
namespace RoyalIdentity.Contracts.Models.Messages;

public class ErrorMessage
{
    public string? Error { get; set; }

    public string? ErrorDescription { get; set; }

    public string? RequestId { get; set; }
}

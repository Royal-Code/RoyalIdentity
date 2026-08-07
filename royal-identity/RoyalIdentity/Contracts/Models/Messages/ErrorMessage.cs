// This file contains material derived from IdentityServer4 and/or IdentityModel.
// Original component copyrights remain with Brock Allen, Dominick Baier, and/or Duende Software.
// Licensed under Apache License 2.0; see LICENSES/Apache-2.0.txt and THIRD-PARTY-NOTICES.md.
// Modified by RoyalIdentity contributors for the RoyalIdentity rearchitecture.
//
namespace RoyalIdentity.Contracts.Models.Messages;

public class ErrorMessage
{
    public string? Error { get; set; }

    /// <summary>
    /// A literal description, already in its final form. Protocol descriptions travel here and are never
    /// translated (plan-localization DF11).
    /// </summary>
    public string? ErrorDescription { get; set; }

    /// <summary>
    /// A presentation message code the UI resolves in the reader's culture. Kept apart from
    /// <see cref="ErrorDescription"/> on purpose: a single field carrying either a sentence or a key cannot be
    /// rendered safely, and printing the key is exactly the defect this separation prevents.
    /// </summary>
    public string? MessageCode { get; set; }

    public string? RequestId { get; set; }
}

using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using RoyalIdentity.Options;
using System.Collections.ObjectModel;

namespace RoyalIdentity.Models.Security;

/// <summary>
/// A pure, deterministic assessment of security facts observable in a <see cref="Client"/> and its
/// <see cref="RealmOptions"/>.
/// </summary>
/// <remarks>
/// <see cref="SecurityAssessmentStatus.Compliant"/> means that no applicable configuration finding was produced.
/// It is not a certification of TLS, proxies, headers, network topology, resource servers or deployment posture.
/// The assessment performs no I/O and is not an authorization decision.
/// </remarks>
public sealed class ClientSecurityAssessment
{
    private ClientSecurityAssessment(List<ClientSecurityFinding> findings)
    {
        Findings = new ReadOnlyCollection<ClientSecurityFinding>(findings);
        Status = findings.Any(finding => finding.Status is SecurityAssessmentStatus.NonCompliant)
            ? SecurityAssessmentStatus.NonCompliant
            : findings.Count > 0
                ? SecurityAssessmentStatus.Warning
                : SecurityAssessmentStatus.Compliant;
    }

    /// <summary>Gets the status derived from <see cref="Findings"/>.</summary>
    public SecurityAssessmentStatus Status { get; }

    /// <summary>Gets the findings in deterministic rule order.</summary>
    public IReadOnlyList<ClientSecurityFinding> Findings { get; }

    /// <summary>
    /// Creates an assessment from the current client and realm configuration without DI, I/O or persistence.
    /// </summary>
    /// <param name="client">The client to assess.</param>
    /// <param name="realmOptions">The effective options of the client's realm.</param>
    /// <returns>A derived, ephemeral assessment.</returns>
    public static ClientSecurityAssessment Create(Client client, RealmOptions realmOptions)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(realmOptions);

        var findings = new List<ClientSecurityFinding>();

        EvaluateRedirectUris(client, findings);
        EvaluatePkce(client, findings);
        EvaluateFrontChannelTokens(client, findings);
        EvaluatePasswordGrant(client, findings);
        EvaluatePublicClientRefreshTokens(client, findings);
        EvaluateClientAuthentication(client, realmOptions, findings);
        EvaluateSenderConstraint(client, realmOptions, findings);

        return new ClientSecurityAssessment(findings);
    }

    private static void EvaluateRedirectUris(Client client, List<ClientSecurityFinding> findings)
    {
        if (client.RedirectUris.Any(uri => uri?.Contains('*', StringComparison.Ordinal) is true))
        {
            findings.Add(new ClientSecurityFinding(
                ClientSecurityRuleIds.RedirectExactMatch,
                SecurityRequirementLevel.Must,
                SecurityAssessmentStatus.NonCompliant,
                SecurityFindingSeverity.Critical,
                "The client registers a wildcard authorization redirect URI, so matching is not exact.",
                "Register every authorization redirect URI explicitly and use exact ordinal matching."));
        }

        if (client.RedirectUris.Any(IsNonAbsoluteAuthorizationRedirect))
        {
            findings.Add(new ClientSecurityFinding(
                ClientSecurityRuleIds.AbsoluteAuthorizationRedirect,
                SecurityRequirementLevel.Must,
                SecurityAssessmentStatus.NonCompliant,
                SecurityFindingSeverity.Critical,
                "The client registers an authorization redirect URI that is not absolute.",
                "Replace the redirect URI with an absolute URI."));
        }

        if (client.RedirectUris.Any(HasAuthorizationRedirectFragment))
        {
            findings.Add(new ClientSecurityFinding(
                ClientSecurityRuleIds.NoAuthorizationRedirectFragment,
                SecurityRequirementLevel.MustNot,
                SecurityAssessmentStatus.NonCompliant,
                SecurityFindingSeverity.Critical,
                "The client registers an authorization redirect URI that contains a fragment.",
                "Remove the fragment from the redirect URI."));
        }

        if (client.RedirectUris.Any(IsUnencryptedAuthorizationRedirect))
        {
            findings.Add(new ClientSecurityFinding(
                ClientSecurityRuleIds.HttpsAuthorizationRedirect,
                SecurityRequirementLevel.MustNot,
                SecurityAssessmentStatus.NonCompliant,
                SecurityFindingSeverity.Critical,
                "The client registers an authorization redirect URI that does not use HTTPS.",
                "Replace the redirect URI with an HTTPS URI."));
        }
    }

    private static void EvaluatePkce(Client client, List<ClientSecurityFinding> findings)
    {
        if (!UsesAuthorizationCode(client))
            return;

        if (!client.RequirePkce)
        {
            var publicClient = client.ClientType is ClientType.Public;
            findings.Add(new ClientSecurityFinding(
                ClientSecurityRuleIds.Pkce,
                publicClient ? SecurityRequirementLevel.Must : SecurityRequirementLevel.Recommended,
                publicClient ? SecurityAssessmentStatus.NonCompliant : SecurityAssessmentStatus.Warning,
                publicClient ? SecurityFindingSeverity.High : SecurityFindingSeverity.Medium,
                publicClient
                    ? "The public authorization-code client does not require PKCE."
                    : "The confidential authorization-code client does not require PKCE.",
                "Require PKCE for every authorization-code request."));
        }

        if (client.AllowPlainTextPkce)
        {
            findings.Add(new ClientSecurityFinding(
                ClientSecurityRuleIds.PkceS256,
                SecurityRequirementLevel.Should,
                SecurityAssessmentStatus.Warning,
                SecurityFindingSeverity.Medium,
                "The client permits the plain PKCE challenge method, which exposes the verifier-derived challenge.",
                "Disable plain PKCE and use S256."));
        }
    }

    private static void EvaluateFrontChannelTokens(Client client, List<ClientSecurityFinding> findings)
    {
        if (!client.AllowedResponseTypes.Any(ContainsAccessTokenResponse))
            return;

        findings.Add(new ClientSecurityFinding(
            ClientSecurityRuleIds.NoFrontChannelAccessToken,
            SecurityRequirementLevel.ShouldNot,
            SecurityAssessmentStatus.Warning,
            SecurityFindingSeverity.High,
            "The client permits a response type that issues an access token in the authorization response.",
            "Use the code response type and obtain access tokens from the token endpoint."));
    }

    private static void EvaluatePasswordGrant(Client client, List<ClientSecurityFinding> findings)
    {
        if (client.AllowedGrantTypes?.Contains(OpenIdConnectGrantTypes.Password) is not true)
            return;

        findings.Add(new ClientSecurityFinding(
            ClientSecurityRuleIds.NoPasswordGrant,
            SecurityRequirementLevel.MustNot,
            SecurityAssessmentStatus.NonCompliant,
            SecurityFindingSeverity.Critical,
            "The client configuration permits the resource owner password credentials grant.",
            "Remove the password grant and use an interactive or workload-appropriate flow."));
    }

    private static void EvaluatePublicClientRefreshTokens(Client client, List<ClientSecurityFinding> findings)
    {
        if (client.ClientType is not ClientType.Public || !client.AllowOfflineAccess)
            return;

        findings.Add(new ClientSecurityFinding(
            ClientSecurityRuleIds.PublicRefreshTokenReplayProtection,
            SecurityRequirementLevel.Must,
            SecurityAssessmentStatus.NonCompliant,
            SecurityFindingSeverity.High,
            "The public client can receive refresh tokens before rotation or sender constraint is enforced for that grant.",
            "Use refresh token rotation with family replay detection or a sender-constrained refresh token."));
    }

    private static void EvaluateClientAuthentication(
        Client client,
        RealmOptions realmOptions,
        List<ClientSecurityFinding> findings)
    {
        if (client.ClientType is not ClientType.Confidential)
            return;

        if (!client.RequireClientSecret)
        {
            findings.Add(new ClientSecurityFinding(
                ClientSecurityRuleIds.ClientAuthentication,
                SecurityRequirementLevel.Should,
                SecurityAssessmentStatus.Warning,
                SecurityFindingSeverity.High,
                "The confidential client is configured without mandatory client authentication.",
                "Require client authentication and register a credential appropriate for the deployment."));
            return;
        }

        if (HasAsymmetricClientCredential(client, realmOptions))
            return;

        findings.Add(new ClientSecurityFinding(
            ClientSecurityRuleIds.AsymmetricClientAuthentication,
            SecurityRequirementLevel.Recommended,
            SecurityAssessmentStatus.Warning,
            SecurityFindingSeverity.Medium,
            "The confidential client has no observable asymmetric authentication credential.",
            "Register a private_key_jwt or mutual-TLS credential instead of relying only on a shared secret."));
    }

    private static void EvaluateSenderConstraint(
        Client client,
        RealmOptions realmOptions,
        List<ClientSecurityFinding> findings)
    {
        if (HasObservableSenderConstraint(client, realmOptions))
            return;

        findings.Add(new ClientSecurityFinding(
            ClientSecurityRuleIds.SenderConstrainedAccessTokens,
            SecurityRequirementLevel.Should,
            SecurityAssessmentStatus.Warning,
            SecurityFindingSeverity.Medium,
            "No supported observable configuration sender-constrains access tokens for this client.",
            client.ClientType is ClientType.Public
                ? "Adopt DPoP when support is implemented; the current runtime exposes no sender-constraint mechanism for public clients."
                : "Enable mutual TLS and register an mTLS client credential, or adopt another sender-constraint mechanism."));
    }

    private static bool UsesAuthorizationCode(Client client)
        => client.AllowedGrantTypes?.Contains(OpenIdConnectGrantTypes.AuthorizationCode) is true;

    private static bool ContainsAccessTokenResponse(string? responseType)
        => responseType?
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Contains(Oidc.ResponseTypes.Token, StringComparer.Ordinal) is true;

    private static bool IsNonAbsoluteAuthorizationRedirect(string? redirectUri)
        => string.IsNullOrWhiteSpace(redirectUri)
            || !Uri.TryCreate(redirectUri, UriKind.Absolute, out _);

    private static bool HasAuthorizationRedirectFragment(string? redirectUri)
        => Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri)
            && !string.IsNullOrEmpty(uri.Fragment);

    private static bool IsUnencryptedAuthorizationRedirect(string? redirectUri)
        => Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri)
            && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

    private static bool HasAsymmetricClientCredential(Client client, RealmOptions realmOptions)
        => client.ClientSecrets?.Any(secret =>
            IsPrivateKeyJwtCredential(secret)
            || realmOptions.MutualTls.Enabled && IsMutualTlsCredential(secret)) is true;

    private static bool HasMutualTlsCredential(Client client)
        => client.ClientSecrets?.Any(IsMutualTlsCredential) is true;

    private static bool HasObservableSenderConstraint(Client client, RealmOptions realmOptions)
        => realmOptions.MutualTls.Enabled
            && client.RequireClientSecret
            && HasMutualTlsCredential(client)
            && client.ClientSecrets!.All(IsMutualTlsCredential);

    private static bool IsMutualTlsCredential(ClientSecret? secret)
        => string.Equals(secret?.Type, Server.SecretTypes.X509CertificateThumbprint, StringComparison.Ordinal)
            || string.Equals(secret?.Type, Server.SecretTypes.X509CertificateName, StringComparison.Ordinal);

    private static bool IsPrivateKeyJwtCredential(ClientSecret? secret)
        => string.Equals(secret?.Type, Server.SecretTypes.JsonWebKey, StringComparison.Ordinal)
            || string.Equals(secret?.Type, Server.SecretTypes.X509CertificateBase64, StringComparison.Ordinal);
}

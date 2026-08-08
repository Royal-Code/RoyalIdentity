using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Security;
using RoyalIdentity.Options;
using System.Reflection;

namespace Tests.Identity.Security;

public class ClientSecurityAssessmentTests
{
    [Fact]
    public void Create_WithTheSecureObservableConfiguration_IsCompliant()
    {
        var (client, options) = CreateSecureConfiguration();

        var assessment = ClientSecurityAssessment.Create(client, options);

        Assert.Equal(SecurityAssessmentStatus.Compliant, assessment.Status);
        Assert.Empty(assessment.Findings);
    }

    [Theory]
    [InlineData(Violation.WildcardRedirect, ClientSecurityRuleIds.RedirectExactMatch,
        SecurityRequirementLevel.Must, SecurityAssessmentStatus.NonCompliant, SecurityFindingSeverity.Critical)]
    [InlineData(Violation.HttpRedirect, ClientSecurityRuleIds.HttpsAuthorizationRedirect,
        SecurityRequirementLevel.MustNot, SecurityAssessmentStatus.NonCompliant, SecurityFindingSeverity.Critical)]
    [InlineData(Violation.NonAbsoluteRedirect, ClientSecurityRuleIds.AbsoluteAuthorizationRedirect,
        SecurityRequirementLevel.Must, SecurityAssessmentStatus.NonCompliant, SecurityFindingSeverity.Critical)]
    [InlineData(Violation.FragmentRedirect, ClientSecurityRuleIds.NoAuthorizationRedirectFragment,
        SecurityRequirementLevel.MustNot, SecurityAssessmentStatus.NonCompliant, SecurityFindingSeverity.Critical)]
    [InlineData(Violation.PublicClientWithoutPkce, ClientSecurityRuleIds.Pkce,
        SecurityRequirementLevel.Must, SecurityAssessmentStatus.NonCompliant, SecurityFindingSeverity.High)]
    [InlineData(Violation.ConfidentialClientWithoutPkce, ClientSecurityRuleIds.Pkce,
        SecurityRequirementLevel.Recommended, SecurityAssessmentStatus.Warning, SecurityFindingSeverity.Medium)]
    [InlineData(Violation.PlainPkce, ClientSecurityRuleIds.PkceS256,
        SecurityRequirementLevel.Should, SecurityAssessmentStatus.Warning, SecurityFindingSeverity.Medium)]
    [InlineData(Violation.FrontChannelAccessToken, ClientSecurityRuleIds.NoFrontChannelAccessToken,
        SecurityRequirementLevel.ShouldNot, SecurityAssessmentStatus.Warning, SecurityFindingSeverity.High)]
    [InlineData(Violation.PasswordGrant, ClientSecurityRuleIds.NoPasswordGrant,
        SecurityRequirementLevel.MustNot, SecurityAssessmentStatus.NonCompliant, SecurityFindingSeverity.Critical)]
    [InlineData(Violation.PublicClientRefreshToken, ClientSecurityRuleIds.PublicRefreshTokenReplayProtection,
        SecurityRequirementLevel.Must, SecurityAssessmentStatus.NonCompliant, SecurityFindingSeverity.High)]
    [InlineData(Violation.ConfidentialClientWithoutAuthentication, ClientSecurityRuleIds.ClientAuthentication,
        SecurityRequirementLevel.Should, SecurityAssessmentStatus.Warning, SecurityFindingSeverity.High)]
    [InlineData(Violation.SymmetricClientAuthentication, ClientSecurityRuleIds.AsymmetricClientAuthentication,
        SecurityRequirementLevel.Recommended, SecurityAssessmentStatus.Warning, SecurityFindingSeverity.Medium)]
    [InlineData(Violation.AccessTokenWithoutSenderConstraint, ClientSecurityRuleIds.SenderConstrainedAccessTokens,
        SecurityRequirementLevel.Should, SecurityAssessmentStatus.Warning, SecurityFindingSeverity.Medium)]
    public void Create_MapsEachObservableViolationToItsNormativeFinding(
        Violation violation,
        string ruleId,
        SecurityRequirementLevel requirementLevel,
        SecurityAssessmentStatus status,
        SecurityFindingSeverity severity)
    {
        var (client, options) = CreateSecureConfiguration();
        Apply(violation, client, options);

        var assessment = ClientSecurityAssessment.Create(client, options);

        var finding = Assert.Single(assessment.Findings, finding => finding.RuleId == ruleId);
        Assert.Equal(requirementLevel, finding.RequirementLevel);
        Assert.Equal(status, finding.Status);
        Assert.Equal(severity, finding.Severity);
        Assert.False(string.IsNullOrWhiteSpace(finding.Description));
        Assert.False(string.IsNullOrWhiteSpace(finding.Remediation));
    }

    [Fact]
    public void Create_NonComplianceTakesPrecedenceOverWarnings()
    {
        var (client, options) = CreateSecureConfiguration();
        client.AllowPlainTextPkce = true;
        client.AllowedGrantTypes.Add(OpenIdConnectGrantTypes.Password);

        var assessment = ClientSecurityAssessment.Create(client, options);

        Assert.Contains(assessment.Findings, finding => finding.Status is SecurityAssessmentStatus.Warning);
        Assert.Contains(assessment.Findings, finding => finding.Status is SecurityAssessmentStatus.NonCompliant);
        Assert.Equal(SecurityAssessmentStatus.NonCompliant, assessment.Status);
    }

    [Fact]
    public void Create_ARecommendationWithoutMandatoryFailure_ProducesWarning()
    {
        var (client, options) = CreateSecureConfiguration();
        options.MutualTls.Enabled = false;

        var assessment = ClientSecurityAssessment.Create(client, options);

        Assert.Equal(SecurityAssessmentStatus.Warning, assessment.Status);
        Assert.All(assessment.Findings, finding =>
            Assert.Equal(SecurityAssessmentStatus.Warning, finding.Status));
    }

    [Fact]
    public void Create_WithTheSameObjects_IsDeterministicAndValueEquivalent()
    {
        var (client, options) = CreateSecureConfiguration();
        client.AllowPlainTextPkce = true;
        client.AllowedGrantTypes.Add(OpenIdConnectGrantTypes.Password);

        var first = ClientSecurityAssessment.Create(client, options);
        var second = ClientSecurityAssessment.Create(client, options);

        Assert.Equal(first.Status, second.Status);
        Assert.Equal(first.Findings, second.Findings);
        Assert.Equal(
            first.Findings.Select(finding => finding.RuleId),
            second.Findings.Select(finding => finding.RuleId));
    }

    [Fact]
    public void Create_RejectsNullInputs()
    {
        var (client, options) = CreateSecureConfiguration();

        Assert.Throws<ArgumentNullException>(() => ClientSecurityAssessment.Create(null!, options));
        Assert.Throws<ArgumentNullException>(() => ClientSecurityAssessment.Create(client, null!));
    }

    [Fact]
    public void RuleIds_AreStableUniqueConstants()
    {
        var fields = typeof(ClientSecurityRuleIds)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral && !field.IsInitOnly)
            .ToArray();
        var values = fields.Select(field => Assert.IsType<string>(field.GetRawConstantValue())).ToArray();
        string[] expected =
        [
            "RFC6749-3.1.2-ABSOLUTE-AUTHORIZATION-REDIRECT",
            "RFC6749-3.1.2-NO-AUTHORIZATION-REDIRECT-FRAGMENT",
            "RFC9700-2.1-REDIRECT-EXACT-MATCH",
            "RFC9700-2.1.1-PKCE",
            "RFC9700-2.1.1-PKCE-S256",
            "RFC9700-2.1.2-NO-FRONTCHANNEL-ACCESS-TOKEN",
            "RFC9700-2.2.1-SENDER-CONSTRAINED-ACCESS-TOKENS",
            "RFC9700-2.2.2-PUBLIC-REFRESH-REPLAY",
            "RFC9700-2.4-NO-PASSWORD-GRANT",
            "RFC9700-2.5-ASYMMETRIC-CLIENT-AUTHENTICATION",
            "RFC9700-2.5-CLIENT-AUTHENTICATION",
            "RFC9700-2.6-HTTPS-AUTHORIZATION-REDIRECT",
        ];

        Assert.Equal(12, fields.Length);
        Assert.Equal(values.Length, values.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(expected, values.Order(StringComparer.Ordinal));
        Assert.All(values, value => Assert.StartsWith("RFC", value, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Base64Certificate_IsPrivateKeyJwtCredentialButNeverAnMtlsCredential(bool mutualTlsEnabled)
    {
        var (client, options) = CreateSecureConfiguration();
        options.MutualTls.Enabled = mutualTlsEnabled;
        client.ClientSecrets.Clear();
        client.ClientSecrets.Add(new ClientSecret("base64-certificate")
        {
            Type = Constants.Server.SecretTypes.X509CertificateBase64,
        });

        var assessment = ClientSecurityAssessment.Create(client, options);

        Assert.DoesNotContain(assessment.Findings,
            finding => finding.RuleId == ClientSecurityRuleIds.AsymmetricClientAuthentication);
        Assert.Contains(assessment.Findings,
            finding => finding.RuleId == ClientSecurityRuleIds.SenderConstrainedAccessTokens);
    }

    [Fact]
    public void PublicClientSenderConstraintFinding_NamesTheDeferredSupportedMechanism()
    {
        var (client, options) = CreateSecureConfiguration();
        client.ClientType = ClientType.Public;
        client.RequireClientSecret = false;

        var assessment = ClientSecurityAssessment.Create(client, options);

        var finding = Assert.Single(assessment.Findings,
            finding => finding.RuleId == ClientSecurityRuleIds.SenderConstrainedAccessTokens);
        Assert.Contains("DPoP", finding.Remediation, StringComparison.Ordinal);
        Assert.Contains("current runtime", finding.Remediation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PublicContract_HasNoClockOrEvaluatorMetadata()
    {
        var properties = typeof(ClientSecurityAssessment)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ToArray();

        Assert.Equal([nameof(ClientSecurityAssessment.Status), nameof(ClientSecurityAssessment.Findings)], properties);
        Assert.DoesNotContain("AssessedAt", properties);
        Assert.DoesNotContain("EvaluatorVersion", properties);
    }

    [Fact]
    public void AssessmentAndFindings_AreImmutableToConsumers()
    {
        var (client, options) = CreateSecureConfiguration();
        client.AllowPlainTextPkce = true;
        var assessment = ClientSecurityAssessment.Create(client, options);
        var finding = Assert.Single(assessment.Findings);

        Assert.All(typeof(ClientSecurityAssessment).GetProperties(), property => Assert.Null(property.SetMethod));
        Assert.All(typeof(ClientSecurityFinding).GetProperties(), property => Assert.Null(property.SetMethod));
        var mutableView = Assert.IsAssignableFrom<ICollection<ClientSecurityFinding>>(assessment.Findings);
        Assert.Throws<NotSupportedException>(() => mutableView.Add(finding));
    }

    private static (Client Client, RealmOptions Options) CreateSecureConfiguration()
    {
        var options = new RealmOptions(new ServerOptions());
        options.MutualTls.Enabled = true;
        var realm = new Realm("assessment", "assessment.example", "assessment", "Assessment", false, options);
        var client = new Client
        {
            Id = "secure-client",
            Name = "Secure client",
            Realm = realm,
            ClientType = ClientType.Confidential,
            RequireClientSecret = true,
            RequirePkce = true,
            AllowPlainTextPkce = false,
            AllowOfflineAccess = false,
        };
        client.RedirectUris.Add("https://client.example/callback");
        client.ClientSecrets.Add(new ClientSecret("certificate-thumbprint")
        {
            Type = Constants.Server.SecretTypes.X509CertificateThumbprint,
        });
        return (client, options);
    }

    private static void Apply(Violation violation, Client client, RealmOptions options)
    {
        switch (violation)
        {
            case Violation.WildcardRedirect:
                client.RedirectUris.Clear();
                client.RedirectUris.Add("https://client.example/**");
                break;
            case Violation.HttpRedirect:
                client.RedirectUris.Clear();
                client.RedirectUris.Add("http://client.example/callback");
                break;
            case Violation.NonAbsoluteRedirect:
                client.RedirectUris.Clear();
                client.RedirectUris.Add("client/callback");
                break;
            case Violation.FragmentRedirect:
                client.RedirectUris.Clear();
                client.RedirectUris.Add("https://client.example/callback#fragment");
                break;
            case Violation.PublicClientWithoutPkce:
                client.ClientType = ClientType.Public;
                client.RequireClientSecret = false;
                client.RequirePkce = false;
                break;
            case Violation.ConfidentialClientWithoutPkce:
                client.RequirePkce = false;
                break;
            case Violation.PlainPkce:
                client.AllowPlainTextPkce = true;
                break;
            case Violation.FrontChannelAccessToken:
                client.AllowedResponseTypes.Add(Constants.Oidc.ResponseTypes.Token);
                break;
            case Violation.PasswordGrant:
                client.AllowedGrantTypes.Add(OpenIdConnectGrantTypes.Password);
                break;
            case Violation.PublicClientRefreshToken:
                client.ClientType = ClientType.Public;
                client.RequireClientSecret = false;
                client.AllowOfflineAccess = true;
                client.AllowedGrantTypes.Add(OpenIdConnectGrantTypes.RefreshToken);
                break;
            case Violation.ConfidentialClientWithoutAuthentication:
                client.RequireClientSecret = false;
                break;
            case Violation.SymmetricClientAuthentication:
                client.ClientSecrets.Clear();
                client.ClientSecrets.Add(new ClientSecret("shared-secret"));
                break;
            case Violation.AccessTokenWithoutSenderConstraint:
                client.ClientSecrets.Clear();
                client.ClientSecrets.Add(new ClientSecret("public-jwk")
                {
                    Type = Constants.Server.SecretTypes.JsonWebKey,
                });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(violation), violation, null);
        }
    }

    public enum Violation
    {
        WildcardRedirect,
        HttpRedirect,
        NonAbsoluteRedirect,
        FragmentRedirect,
        PublicClientWithoutPkce,
        ConfidentialClientWithoutPkce,
        PlainPkce,
        FrontChannelAccessToken,
        PasswordGrant,
        PublicClientRefreshToken,
        ConfidentialClientWithoutAuthentication,
        SymmetricClientAuthentication,
        AccessTokenWithoutSenderConstraint,
    }
}

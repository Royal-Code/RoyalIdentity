using System.Security.Claims;
using System.Text.Json.Nodes;
using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Storage.EntityFramework.Operational.Materialization;
using Tests.Storage.Operational.Support;

namespace Tests.Storage.Operational;

/// <summary>
/// Versioned payload serialization of the Operational models (plan-data-operational-storage Fase 1, DF9/DF34):
/// round-trips are complete and stable, materialization is independent, values that live in queryable columns
/// are never duplicated in the payload (DF36), the raw handles never reach it either (DF13/DF38), the
/// persisted claim contract is the minimal one (DF34) — while the models' own properties keep round-tripping
/// — and an unknown version, malformed JSON, a missing contract member or an explicit null fails closed
/// instead of returning a partial model.
/// </summary>
public class OperationalPayloadTests
{
    private readonly AccessTokenPayloadSerializer accessTokens = new();
    private readonly RefreshTokenPayloadSerializer refreshTokens = new();
    private readonly AuthorizationCodePayloadSerializer authorizationCodes = new();
    private readonly ConsentPayloadSerializer consents = new();
    private readonly AuthorizeParametersPayloadSerializer authorizeParameters = new();

    [Fact]
    public void AccessToken_RoundTrip_IsCompleteAndStable()
    {
        var token = OperationalTestData.NewReferenceAccessToken();
        var identity = OperationalTestData.IdentityOf(token);

        var (version, json) = accessTokens.Serialize(token);
        var restored = accessTokens.Deserialize(version, json, identity);
        var (_, reserialized) = accessTokens.Serialize(restored);

        Assert.Equal(AccessTokenPayloadSerializer.CurrentVersion, version);
        Assert.Equal(json, reserialized);
        Assert.Equal(token.Id, restored.Id);
        Assert.Equal(token.ClientId, restored.ClientId);
        Assert.Equal(token.Issuer, restored.Issuer);
        Assert.Equal(token.TokenType, restored.TokenType);
        Assert.Equal(token.AccessTokenType, restored.AccessTokenType);
        Assert.Equal(token.CreationTime, restored.CreationTime);
        Assert.Equal(token.Lifetime, restored.Lifetime);
        Assert.Equal(token.RealmId, restored.RealmId);
        Assert.Equal(token.Confirmation, restored.Confirmation);
        Assert.Equal(token.Audiences, restored.Audiences);
        Assert.Equal(token.AllowedSigningAlgorithms, restored.AllowedSigningAlgorithms);
        Assert.Equal(token.ResourceUris, restored.ResourceUris);
        Assert.Equal(
            token.Claims.Select(c => (c.Type, c.Value, c.ValueType)).OrderBy(c => c.Type),
            restored.Claims.Select(c => (c.Type, c.Value, c.ValueType)).OrderBy(c => c.Type));
    }

    // DF9/DF36: whatever a column holds is authoritative and must not exist a second time in the payload —
    // a divergence would let a lookup or a conditional consumption validate one value while the materialized
    // object carried another.
    [Fact]
    public void Payloads_DoNotDuplicateAnyQueryableColumn()
    {
        var token = OperationalTestData.NewReferenceAccessToken();
        var refreshToken = OperationalTestData.NewRefreshToken();
        var code = OperationalTestData.NewAuthorizationCode();

        var (_, tokenJson) = accessTokens.Serialize(token);
        var (_, refreshJson) = refreshTokens.Serialize(refreshToken);
        var (_, codeJson) = authorizationCodes.Serialize(code);

        foreach (var json in new[] { tokenJson, refreshJson, codeJson })
        {
            Assert.DoesNotContain("\"ClientId\"", json, StringComparison.Ordinal);
            Assert.DoesNotContain("\"RealmId\"", json, StringComparison.Ordinal);
            Assert.DoesNotContain("\"CreationTime\"", json, StringComparison.Ordinal);
            Assert.DoesNotContain("\"Lifetime\"", json, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("\"AccessTokenType\"", tokenJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"RedirectUri\"", codeJson, StringComparison.Ordinal);
        Assert.DoesNotContain("\"SessionId\"", codeJson, StringComparison.Ordinal);
    }

    // The columns are the source, so the materialized object reflects them even when they disagree with what
    // the model held at write time. This is the falsifiable form of the invariant above.
    [Fact]
    public void Materialization_TakesTheQueryableValuesFromTheColumns()
    {
        var token = OperationalTestData.NewReferenceAccessToken();
        var (version, json) = accessTokens.Serialize(token);

        var identity = new AccessTokenIdentity(
            "other-jti",
            "realm-b",
            "client-two",
            AccessTokenType.Jwt,
            OperationalTestData.CreationTime.AddHours(1),
            OperationalTestData.CreationTime.AddHours(3));

        var restored = accessTokens.Deserialize(version, json, identity);

        Assert.Equal("other-jti", restored.Id);
        Assert.Equal("realm-b", restored.RealmId);
        Assert.Equal("client-two", restored.ClientId);
        Assert.Equal(AccessTokenType.Jwt, restored.AccessTokenType);
        Assert.Equal(OperationalTestData.CreationTime.AddHours(1), restored.CreationTime);
        Assert.Equal(7200, restored.Lifetime);
    }

    // The conditional consumption of DF11 matches client and redirect URI in the database; the object handed
    // to the pipeline must carry exactly those values.
    [Fact]
    public void AuthorizationCode_TakesClientAndRedirectUriFromTheColumns()
    {
        var code = OperationalTestData.NewAuthorizationCode();
        var (version, json) = authorizationCodes.Serialize(code);

        var identity = new AuthorizationCodeIdentity(
            "code-handle",
            "realm-b",
            "client-two",
            "https://other.example/callback",
            "session-two",
            OperationalTestData.CreationTime,
            OperationalTestData.CreationTime.AddMinutes(5));

        var restored = authorizationCodes.Deserialize(version, json, identity);

        Assert.Equal("client-two", restored.ClientId);
        Assert.Equal("https://other.example/callback", restored.RedirectUri);
        Assert.Equal("session-two", restored.SessionId);
        Assert.Equal("realm-b", restored.RealmId);
        Assert.Equal(300, restored.Lifetime);
    }

    // An expiration that precedes the creation instant cannot produce a coherent lifetime.
    [Fact]
    public void IncoherentTimestamps_FailClosed()
    {
        var token = OperationalTestData.NewReferenceAccessToken();
        var (version, json) = accessTokens.Serialize(token);

        var identity = new AccessTokenIdentity(
            token.Id,
            "realm-a",
            token.ClientId,
            token.AccessTokenType,
            OperationalTestData.CreationTime,
            OperationalTestData.CreationTime.AddSeconds(-1));

        Assert.Throws<OperationalPayloadException>(() => accessTokens.Deserialize(version, json, identity));
    }

    // DF13/DF38: the reference bearer coincides with the jti, which is the lookup argument, so it is never
    // copied into the payload; materialization restores both Id and Token from that argument.
    [Fact]
    public void ReferenceAccessToken_DoesNotPersistItsBearer_AndIsRematerializedFromTheJti()
    {
        var token = OperationalTestData.NewReferenceAccessToken("reference-bearer-value");

        var (version, json) = accessTokens.Serialize(token);
        var restored = accessTokens.Deserialize(version, json, OperationalTestData.IdentityOf(token));

        Assert.DoesNotContain("reference-bearer-value", json, StringComparison.Ordinal);
        Assert.Equal("reference-bearer-value", restored.Id);
        Assert.Equal("reference-bearer-value", restored.Token);
    }

    // DF31: a JWT persisted in Full mode keeps its compact form, which differs from the jti and is never the
    // lookup key.
    [Fact]
    public void JwtAccessToken_InFullMode_RoundTripsTheCompactToken()
    {
        var token = OperationalTestData.NewReferenceAccessToken("jwt-jti");
        token.Token = "header.payload.signature";

        var (version, json) = accessTokens.Serialize(token);
        var restored = accessTokens.Deserialize(version, json, OperationalTestData.IdentityOf(token));

        Assert.Equal("jwt-jti", restored.Id);
        Assert.Equal("header.payload.signature", restored.Token);
    }

    [Fact]
    public void RefreshToken_RoundTrip_IsCompleteAndStable()
    {
        var token = OperationalTestData.NewRefreshToken();

        var (version, json) = refreshTokens.Serialize(token);
        var restored = refreshTokens.Deserialize(version, json, OperationalTestData.IdentityOf(token));
        var (_, reserialized) = refreshTokens.Serialize(restored);

        Assert.Equal(RefreshTokenPayloadSerializer.CurrentVersion, version);
        Assert.Equal(json, reserialized);
        Assert.Equal(token.Token, restored.Token);
        Assert.Equal(token.ClientId, restored.ClientId);
        Assert.Equal(token.Issuer, restored.Issuer);
        Assert.Equal(token.CreationTime, restored.CreationTime);
        Assert.Equal(token.Lifetime, restored.Lifetime);
        Assert.Equal(token.RealmId, restored.RealmId);
        Assert.Equal(token.Confirmation, restored.Confirmation);
        Assert.Equal(token.RequestedScopes, restored.RequestedScopes);
        Assert.Equal(token.ResourceUris, restored.ResourceUris);
        Assert.Equal(token.Audiences, restored.Audiences);
        Assert.Equal(token.AllowedSigningAlgorithms, restored.AllowedSigningAlgorithms);
        Assert.Equal(
            token.Claims.Select(c => (c.Type, c.Value)).OrderBy(c => c.Type),
            restored.Claims.Select(c => (c.Type, c.Value)).OrderBy(c => c.Type));
        Assert.Equal("subject-one", restored.SubjectId);
        Assert.Equal("session-one", restored.SessionId);
    }

    // DF38: the refresh handle is the lookup argument, never persisted content.
    [Fact]
    public void RefreshToken_DoesNotPersistItsHandle()
    {
        var token = OperationalTestData.NewRefreshToken("refresh-handle-value");

        var (_, json) = refreshTokens.Serialize(token);

        Assert.DoesNotContain("refresh-handle-value", json, StringComparison.Ordinal);
    }

    [Fact]
    public void AuthorizationCode_RoundTrip_IsCompleteAndStable()
    {
        var code = OperationalTestData.NewAuthorizationCode();

        var (version, json) = authorizationCodes.Serialize(code);
        var restored = authorizationCodes.Deserialize(version, json, OperationalTestData.IdentityOf(code));
        var (_, reserialized) = authorizationCodes.Serialize(restored);

        Assert.Equal(AuthorizationCodePayloadSerializer.CurrentVersion, version);
        Assert.Equal(json, reserialized);
        Assert.Equal(code.Code, restored.Code);
        Assert.Equal(code.ClientId, restored.ClientId);
        Assert.Equal(code.RedirectUri, restored.RedirectUri);
        Assert.Equal(code.SessionState, restored.SessionState);
        Assert.Equal(code.CreationTime, restored.CreationTime);
        Assert.Equal(code.Lifetime, restored.Lifetime);
        Assert.Equal(code.RealmId, restored.RealmId);
        Assert.Equal(code.Nonce, restored.Nonce);
        Assert.Equal(code.StateHash, restored.StateHash);
        Assert.Equal(code.SessionId, restored.SessionId);
        Assert.Equal(code.CodeChallenge, restored.CodeChallenge);
        Assert.Equal(code.CodeChallengeMethod, restored.CodeChallengeMethod);
    }

    // DF38: the raw code is the lookup argument; a store never persists it, and materialization must not mint
    // a new one either.
    [Fact]
    public void AuthorizationCode_DoesNotPersistItsHandle_AndIsRematerializedFromTheLookupArgument()
    {
        var code = OperationalTestData.NewAuthorizationCode();
        var identity = OperationalTestData.IdentityOf(code) with { Code = "code-handle-value" };

        var (version, json) = authorizationCodes.Serialize(code);
        var restored = authorizationCodes.Deserialize(version, json, identity);

        Assert.DoesNotContain(code.Code, json, StringComparison.Ordinal);
        Assert.Equal("code-handle-value", restored.Code);
    }

    // The subject principal survives with its identity metadata and its claims.
    [Fact]
    public void AuthorizationCode_RoundTrip_PreservesTheSubjectPrincipal()
    {
        var code = OperationalTestData.NewAuthorizationCode();

        var (version, json) = authorizationCodes.Serialize(code);
        var restored = authorizationCodes.Deserialize(version, json, OperationalTestData.IdentityOf(code));

        var original = code.Subject.Identities.Single();
        var identity = restored.Subject.Identities.Single();

        Assert.Equal(original.AuthenticationType, identity.AuthenticationType);
        Assert.Equal(original.NameClaimType, identity.NameClaimType);
        Assert.Equal(original.RoleClaimType, identity.RoleClaimType);
        Assert.Equal(
            original.Claims.Select(c => (c.Type, c.Value, c.ValueType)),
            identity.Claims.Select(c => (c.Type, c.Value, c.ValueType)));
    }

    // DF9: the resolved resources are part of the code's operational contract and survive whole.
    [Fact]
    public void AuthorizationCode_RoundTrip_PreservesTheResolvedResources()
    {
        var code = OperationalTestData.NewAuthorizationCode();

        var (version, json) = authorizationCodes.Serialize(code);
        var scopes = authorizationCodes.Deserialize(version, json, OperationalTestData.IdentityOf(code)).Scopes;

        Assert.True(scopes.OfflineAccess);
        Assert.Equal(code.Scopes.RequestedScopeNames, scopes.RequestedScopeNames);
        Assert.Equal(code.Scopes.MissingScopes, scopes.MissingScopes);
        Assert.Equal(code.Scopes.RequestedResourceUris, scopes.RequestedResourceUris);
        Assert.Equal(code.Scopes.InvalidTargets, scopes.InvalidTargets);

        var identityScope = Assert.Single(scopes.IdentityScopes);
        Assert.Equal("profile", identityScope.Name);
        // The IdentityScope constructor would otherwise overwrite Description with DisplayName.
        Assert.Equal("Profile description", identityScope.Description);
        Assert.True(identityScope.Required);
        Assert.True(identityScope.Emphasize);
        Assert.False(identityScope.ShowInDiscoveryDocument);
        Assert.Equal(["name", "family_name"], identityScope.UserClaims);

        var scope = Assert.Single(scopes.Scopes);
        Assert.Equal("api.read", scope.Name);
        Assert.Equal("Read description", scope.Description);
        Assert.False(scope.Enabled);
        Assert.True(scope.ShowInDiscoveryDocument);

        var resourceServer = Assert.Single(scopes.ResourceServers);
        Assert.Equal("https://api.example", resourceServer.Audience);
        Assert.False(resourceServer.AllowScopeRequests);
        Assert.Equal(["RS256", "ES256"], resourceServer.AllowedAccessTokenSigningAlgorithms);
        Assert.Single(resourceServer.Scopes);
        Assert.Single(resourceServer.ProtectedResources);

        var resource = Assert.Single(scopes.ProtectedResources);
        Assert.Equal("https://api.example/orders", resource.ResourceUri);
        Assert.Equal("Orders", resource.DisplayName);
        Assert.Equal("https://docs.example", resource.DocumentationUri);
        Assert.Equal("https://policy.example", resource.PolicyUri);
        Assert.Equal("https://tos.example", resource.TosUri);
        Assert.False(resource.ShowInDiscoveryDocument);

        Assert.Equal(code.Scopes.GetAudiences().Order(), scopes.GetAudiences().Order());
    }

    // DF44: a resource server's own secrets are deliberately outside the operational contract. They never
    // reach the payload, and materialization brings the collection back empty rather than inventing values.
    [Fact]
    public void AuthorizationCode_DoesNotPersistResourceServerSecrets()
    {
        var code = OperationalTestData.NewAuthorizationCodeWithResourceServerSecret("resource-server-secret");

        var (version, json) = authorizationCodes.Serialize(code);
        var scopes = authorizationCodes.Deserialize(version, json, OperationalTestData.IdentityOf(code)).Scopes;

        Assert.DoesNotContain("resource-server-secret", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Secrets", json, StringComparison.Ordinal);
        Assert.NotEmpty(code.Scopes.ResourceServers.Single().Secrets);
        Assert.Empty(scopes.ResourceServers.Single().Secrets);
    }

    // DF34: claim metadata is deliberately outside the contract, but a model's own properties are not — the
    // code's Properties survive the round-trip.
    [Fact]
    public void AuthorizationCode_RoundTrip_PreservesItsOwnProperties()
    {
        var code = OperationalTestData.NewAuthorizationCode();
        code.Properties = new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" };

        var (version, json) = authorizationCodes.Serialize(code);
        var restored = authorizationCodes.Deserialize(version, json, OperationalTestData.IdentityOf(code));

        Assert.NotNull(restored.Properties);
        Assert.Equal("1", restored.Properties["a"]);
        Assert.Equal("2", restored.Properties["b"]);
        Assert.NotSame(code.Properties, restored.Properties);
    }

    // DF34: only Type/Value/ValueType are persisted; Issuer, OriginalIssuer and Claim.Properties are dropped
    // by decision, and the claim comes back with the canonical issuer.
    [Fact]
    public void ClaimPayload_PersistsOnlyTypeValueAndValueType()
    {
        var token = OperationalTestData.NewReferenceAccessToken();
        var claim = new Claim("custom", "value", ClaimValueTypes.String, "https://custom-issuer.example");
        claim.Properties["meta"] = "dropped";
        token.Claims.Add(claim);

        var (version, json) = accessTokens.Serialize(token);
        var restored = accessTokens.Deserialize(version, json, OperationalTestData.IdentityOf(token));
        var restoredClaim = restored.Claims.Single(c => c.Type == "custom");

        Assert.DoesNotContain("https://custom-issuer.example", json, StringComparison.Ordinal);
        Assert.DoesNotContain("dropped", json, StringComparison.Ordinal);
        Assert.Equal("value", restoredClaim.Value);
        Assert.Equal(ClaimValueTypes.String, restoredClaim.ValueType);
        Assert.Equal(ClaimsIdentity.DefaultIssuer, restoredClaim.Issuer);
        Assert.Empty(restoredClaim.Properties);
    }

    // A non-default ValueType is semantic (it drives JSON typing on emission) and is preserved.
    [Fact]
    public void ClaimPayload_PreservesANonDefaultValueType()
    {
        var token = OperationalTestData.NewReferenceAccessToken();

        var (version, json) = accessTokens.Serialize(token);
        var restored = accessTokens.Deserialize(version, json, OperationalTestData.IdentityOf(token));

        Assert.Equal(
            ClaimValueTypes.Integer64,
            restored.Claims.Single(c => c.Type == "auth_time").ValueType);
    }

    [Fact]
    public void Consent_RoundTrip_PreservesScopesAndCasing()
    {
        var consent = OperationalTestData.NewConsent();

        var (version, json) = consents.Serialize(consent);
        var restored = consents.Deserialize(
            version, json, consent.RealmId, consent.SubjectId, consent.ClientId,
            consent.CreationTime, consent.Expiration);

        Assert.Equal(ConsentPayloadSerializer.CurrentVersion, version);
        Assert.Equal(consent.RealmId, restored.RealmId);
        Assert.Equal(consent.SubjectId, restored.SubjectId);
        Assert.Equal(consent.ClientId, restored.ClientId);
        Assert.Equal(consent.CreationTime, restored.CreationTime);
        Assert.Equal(consent.Expiration, restored.Expiration);
        Assert.NotNull(restored.Scopes);
        Assert.Equal(
            consent.Scopes!.Select(s => (s.Scope, s.Description, s.CreationTime, s.JustOnce)),
            restored.Scopes!.Select(s => (s.Scope, s.Description, s.CreationTime, s.JustOnce)));
        // Scope names compare Ordinal (DF10), so two casings are two distinct consented scopes.
        Assert.Contains(restored.Scopes!, s => s.Scope == "Api.Read");
        Assert.Contains(restored.Scopes!, s => s.Scope == "api.read");
    }

    // A consent whose scope collection was never set is distinct from one with an empty collection.
    [Fact]
    public void Consent_RoundTrip_DistinguishesNullScopesFromEmpty()
    {
        var consent = OperationalTestData.NewConsent();
        consent.Scopes = null;

        var (version, json) = consents.Serialize(consent);
        var restored = consents.Deserialize(
            version, json, consent.RealmId, consent.SubjectId, consent.ClientId, consent.CreationTime, null);

        Assert.Null(restored.Scopes);

        consent.Scopes = [];
        var (emptyVersion, emptyJson) = consents.Serialize(consent);
        var restoredEmpty = consents.Deserialize(
            emptyVersion, emptyJson, consent.RealmId, consent.SubjectId, consent.ClientId, consent.CreationTime, null);

        Assert.NotNull(restoredEmpty.Scopes);
        Assert.Empty(restoredEmpty.Scopes!);
    }

    [Fact]
    public void AuthorizeParameters_RoundTrip_PreservesRepeatedKeys()
    {
        var parameters = OperationalTestData.NewAuthorizeParameters();

        var (version, json) = authorizeParameters.Serialize(parameters);
        var restored = authorizeParameters.Deserialize(version, json);

        Assert.Equal(AuthorizeParametersPayloadSerializer.CurrentVersion, version);
        Assert.Equal(parameters.Count, restored.Count);
        Assert.Equal("client-one", restored["client_id"]);
        Assert.Equal("code", restored["response_type"]);
        Assert.Equal("openid profile", restored["scope"]);
        Assert.Equal(
            ["https://api.example/orders", "https://api.example/invoices"],
            restored.GetValues("resource")!);
    }

    // DF9: materialization is independent — mutating what came back never reaches the persisted payload.
    [Fact]
    public void Materialization_ProducesAnIndependentGraph()
    {
        var token = OperationalTestData.NewReferenceAccessToken();
        var identity = OperationalTestData.IdentityOf(token);

        var (version, json) = accessTokens.Serialize(token);
        var first = accessTokens.Deserialize(version, json, identity);
        first.Audiences.Add("https://injected.example");
        first.ResourceUris.Add("https://injected.example/resource");
        var second = accessTokens.Deserialize(version, json, identity);

        Assert.DoesNotContain("https://injected.example", second.Audiences);
        Assert.DoesNotContain("https://injected.example/resource", second.ResourceUris);
        Assert.DoesNotContain("https://injected.example", token.Audiences);
    }

    public static TheoryData<string, Func<int, string, object>> DeserializeCases()
    {
        var accessTokens = new AccessTokenPayloadSerializer();
        var refreshTokens = new RefreshTokenPayloadSerializer();
        var authorizationCodes = new AuthorizationCodePayloadSerializer();
        var consents = new ConsentPayloadSerializer();
        var authorizeParameters = new AuthorizeParametersPayloadSerializer();

        var created = OperationalTestData.CreationTime;
        var expires = created.AddHours(1);

        return new TheoryData<string, Func<int, string, object>>
        {
            {
                "AccessToken",
                (version, json) => accessTokens.Deserialize(
                    version, json, new AccessTokenIdentity("jti", "realm", "client", AccessTokenType.Reference, created, expires))
            },
            {
                "RefreshToken",
                (version, json) => refreshTokens.Deserialize(
                    version, json, new RefreshTokenIdentity("handle", "realm", "client", created, expires))
            },
            {
                "AuthorizationCode",
                (version, json) => authorizationCodes.Deserialize(
                    version, json,
                    new AuthorizationCodeIdentity("code", "realm", "client", "https://c.example/cb", null, created, expires))
            },
            {
                "Consent",
                (version, json) => consents.Deserialize(version, json, "realm", "subject", "client", created, null)
            },
            { "AuthorizeParameters", (version, json) => authorizeParameters.Deserialize(version, json) },
        };
    }

    [Theory]
    [MemberData(nameof(DeserializeCases))]
    public void UnknownVersion_FailsClosed(string _, Func<int, string, object> deserialize)
        => Assert.Throws<OperationalPayloadException>(() => deserialize(99, "{}"));

    [Theory]
    [MemberData(nameof(DeserializeCases))]
    public void MalformedJson_FailsClosed(string _, Func<int, string, object> deserialize)
        => Assert.Throws<OperationalPayloadException>(() => deserialize(1, "{ not json"));

    // A well-formed payload missing a required scalar is incomplete, never a partially materialized model.
    [Fact]
    public void MissingRequiredMember_FailsClosed()
        => Assert.Throws<OperationalPayloadException>(() => Deserialize("""{"Issuer":"x"}"""));

    // An omitted contract collection must fail closed too: silently materializing it as empty would drop
    // audiences, resource URIs or claims without anyone noticing.
    [Theory]
    [InlineData("Audiences")]
    [InlineData("AllowedSigningAlgorithms")]
    [InlineData("ResourceUris")]
    [InlineData("Claims")]
    public void OmittedContractCollection_FailsClosed(string omittedMember)
    {
        var token = OperationalTestData.NewReferenceAccessToken();
        var (_, json) = accessTokens.Serialize(token);

        Assert.Throws<OperationalPayloadException>(() => Deserialize(WithoutMember(json, omittedMember)));
    }

    [Theory]
    [InlineData("Audiences")]
    [InlineData("AllowedSigningAlgorithms")]
    [InlineData("ResourceUris")]
    [InlineData("Claims")]
    public void NullContractCollection_FailsClosed(string nulledMember)
    {
        var token = OperationalTestData.NewReferenceAccessToken();
        var (_, json) = accessTokens.Serialize(token);

        Assert.Throws<OperationalPayloadException>(
            () => Deserialize(WithMemberSetToNull(json, nulledMember)));
    }

    // The same rule applies inside the nested graph of an authorization code.
    [Theory]
    [InlineData("Identities")]
    [InlineData("Claims")]
    [InlineData("RequestedScopeNames")]
    [InlineData("IdentityScopes")]
    [InlineData("ResourceServers")]
    [InlineData("UserClaims")]
    public void OmittedNestedContractCollection_FailsClosed(string omittedMember)
    {
        var code = OperationalTestData.NewAuthorizationCode();
        var (version, json) = authorizationCodes.Serialize(code);
        var identity = OperationalTestData.IdentityOf(code);

        Assert.Throws<OperationalPayloadException>(
            () => authorizationCodes.Deserialize(version, WithoutMember(json, omittedMember), identity));
    }

    // DF9: an authorization code whose subject has no identity cannot produce an empty principal.
    [Fact]
    public void AuthorizationCode_WithoutASubjectIdentity_FailsClosed()
    {
        var code = OperationalTestData.NewAuthorizationCode();
        var (version, json) = authorizationCodes.Serialize(code);

        var node = JsonNode.Parse(json)!;
        node["Subject"]!["Identities"] = new JsonArray();

        Assert.Throws<OperationalPayloadException>(() => authorizationCodes.Deserialize(
            version, node.ToJsonString(), OperationalTestData.IdentityOf(code)));
    }

    // DF9: an identity scope with no user claims cannot be materialized — the model forbids it.
    [Fact]
    public void AuthorizationCode_WithAnIdentityScopeWithoutClaims_FailsClosed()
    {
        var code = OperationalTestData.NewAuthorizationCode();
        var (version, json) = authorizationCodes.Serialize(code);

        var node = JsonNode.Parse(json)!;
        node["Scopes"]!["IdentityScopes"]![0]!["UserClaims"] = new JsonArray();

        Assert.Throws<OperationalPayloadException>(() => authorizationCodes.Deserialize(
            version, node.ToJsonString(), OperationalTestData.IdentityOf(code)));
    }

    private AccessToken Deserialize(string json) => accessTokens.Deserialize(
        AccessTokenPayloadSerializer.CurrentVersion,
        json,
        new AccessTokenIdentity(
            "jti",
            "realm-a",
            "client-one",
            AccessTokenType.Reference,
            OperationalTestData.CreationTime,
            OperationalTestData.CreationTime.AddHours(1)));

    /// <summary>Removes a member from a serialized payload, wherever it appears in the graph.</summary>
    private static string WithoutMember(string json, string member)
        => EditMembers(json, member, (obj, name) => obj.Remove(name));

    /// <summary>Sets a member to an explicit JSON null, wherever it appears in the graph.</summary>
    private static string WithMemberSetToNull(string json, string member)
        => EditMembers(json, member, (obj, name) =>
        {
            if (obj.ContainsKey(name))
                obj[name] = null;
        });

    private static string EditMembers(string json, string member, Action<JsonObject, string> edit)
    {
        var node = JsonNode.Parse(json)!;
        Visit(node);

        return node.ToJsonString();

        void Visit(JsonNode current)
        {
            switch (current)
            {
                case JsonObject obj:
                    edit(obj, member);
                    foreach (var child in obj.Select(pair => pair.Value).Where(value => value is not null).ToList())
                        Visit(child!);
                    break;

                case JsonArray array:
                    foreach (var item in array.Where(value => value is not null).ToList())
                        Visit(item!);
                    break;
            }
        }
    }
}

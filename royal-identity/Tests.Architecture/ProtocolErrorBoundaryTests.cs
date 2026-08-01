using System.Reflection;
using System.Text.RegularExpressions;
using RoyalIdentity.Pipelines.Abstractions;
using RoyalIdentity.Users.Contracts;

namespace Tests.Architecture;

/// <summary>
/// Guards the two halves of the error contract fixed by <c>plan-oauth21-token-error-responses</c>:
/// <c>RoyalIdentity.Pipelines</c> writes error responses but never chooses an OAuth code (DF5/DF19), and the
/// core never lets an error constant occupy the description position of a response helper (DF4).
/// </summary>
public class ProtocolErrorBoundaryTests
{
    private static readonly Assembly Core = typeof(IUserDirectory).Assembly;            // RoyalIdentity
    private static readonly Assembly PipelinesLibrary = typeof(IContextBase).Assembly;  // RoyalIdentity.Pipelines

    private const string CoreName = "RoyalIdentity";
    private const string OidcErrorConstantPattern =
        @"Oidc(?:\.[A-Za-z_][A-Za-z0-9_]*)*\.Errors(?:\.[A-Za-z_][A-Za-z0-9_]*)+";

    /// <summary>
    /// Matches an <c>Oidc.*.Errors.*</c> or <c>Oidc.Errors.*</c> constant sitting where a description belongs —
    /// either as the second positional argument of <c>.Error(</c>, or bound by name to <c>errorDescription</c>.
    /// The first positional argument is where a code belongs and is deliberately not matched.
    /// </summary>
    private static readonly Regex ErrorConstantInDescriptionPosition = new(
        $@"\.Error\(\s*[^,)]+,\s*{OidcErrorConstantPattern}|errorDescription\s*:\s*{OidcErrorConstantPattern}",
        RegexOptions.Compiled);

    /// <summary>
    /// Codes that never existed in any RFC: they were invented so that HTTP-level failures would look like
    /// protocol errors. DF12 removed them, and they must not come back anywhere.
    /// </summary>
    /// <remarks>
    /// Declared before <see cref="ProtocolErrorCodes"/> on purpose: static field initializers run in
    /// declaration order, and the collector below reads this one.
    /// </remarks>
    private static readonly string[] RetiredPseudoCodes =
    [
        "method_not_allowed",
        "invalid_content_type",
        "not_found",
    ];

    /// <summary>
    /// Every protocol error code the product knows, read from where it is defined instead of from a hand
    /// written list. A list maintained by hand drifts: it silently stops covering codes added to
    /// <c>Constants</c> later, and an entry that does not exactly match a literal (<c>content_type</c> against
    /// <c>"invalid_content_type"</c>) never matches anything at all.
    /// </summary>
    public static IReadOnlyCollection<string> ProtocolErrorCodes { get; } = CollectProtocolErrorCodes();

    private static IReadOnlyCollection<string> CollectProtocolErrorCodes()
    {
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Codes declared under any Constants.Oidc.*.Errors group, at any nesting depth.
        var constants = Core.GetType("RoyalIdentity.Options.Constants", throwOnError: true)!;
        CollectConstStrings(constants, insideErrorsGroup: false, codes);

        foreach (var retired in RetiredPseudoCodes)
            codes.Add(retired);

        return codes;
    }

    private static void CollectConstStrings(Type type, bool insideErrorsGroup, HashSet<string> codes)
    {
        if (insideErrorsGroup)
        {
            foreach (var value in ConstStringsOf(type))
                codes.Add(value);
        }

        foreach (var nested in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
        {
            var isErrorsGroup = insideErrorsGroup
                || string.Equals(nested.Name, "Errors", StringComparison.Ordinal);

            CollectConstStrings(nested, isErrorsGroup, codes);
        }
    }

    private static IEnumerable<string> ConstStringsOf(Type type)
    {
        return type
            .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(field => field.IsLiteral && !field.IsInitOnly && field.FieldType == typeof(string))
            .Select(field => (string?)field.GetRawConstantValue())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!);
    }

    /// <summary>
    /// Reports every protocol code written as a string literal in the given source text.
    /// </summary>
    private static IReadOnlyList<string> FindProtocolCodeLiterals(string text, IEnumerable<string> codes)
    {
        return codes
            .Where(code => text.Contains($"\"{code}\"", StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static IEnumerable<(string Path, string Text)> SourceFilesOf(string project)
    {
        var root = Path.Combine(ProjectReferenceReader.FindRepositoryRoot(), project);

        return Directory
            .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Select(path => (Path.GetFileName(path), File.ReadAllText(path)));
    }

    [Fact]
    public void PipelinesLibrary_DoesNotReference_Core()
    {
        var refs = PipelinesLibrary.GetReferencedAssemblies().Select(a => a.Name!);

        Assert.DoesNotContain(refs, n => n == CoreName);
    }

    [Fact]
    public void ProtocolErrorCodes_AreDiscoveredFromTheProduct()
    {
        // Keeps the guard from silently becoming vacuous: if the reflection walk stops finding the Errors
        // groups, the scan below would pass over anything.
        Assert.True(
            ProtocolErrorCodes.Count >= 30,
            $"Expected the Errors groups to yield at least 30 codes, found {ProtocolErrorCodes.Count}.");

        // The codes the neutral project used to hardcode, plus a few the old hand written list missed.
        string[] mustBeCovered =
        [
            "invalid_request",
            "invalid_client",
            "invalid_grant",
            "invalid_scope",
            "invalid_target",
            "unauthorized_client",
            "unsupported_grant_type",
            "server_error",
            "temporarily_unavailable",
            "invalid_request_uri",
            "request_not_supported",
        ];

        foreach (var code in mustBeCovered)
            Assert.Contains(code, ProtocolErrorCodes, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void FindProtocolCodeLiterals_DetectsWhatItClaimsTo()
    {
        // Proof cases. The previous version of this guard listed "content_type" and therefore matched neither
        // the typo it was meant to catch nor its corrected spelling.
        Assert.NotEmpty(FindProtocolCodeLiterals("""x = "Invalid_content_type";""", ProtocolErrorCodes));
        Assert.NotEmpty(FindProtocolCodeLiterals("""x = "invalid_content_type";""", ProtocolErrorCodes));
        Assert.NotEmpty(FindProtocolCodeLiterals("""x = "server_error";""", ProtocolErrorCodes));
        Assert.NotEmpty(FindProtocolCodeLiterals("""Error("invalid_request", d);""", ProtocolErrorCodes));

        // A code arriving as an argument is the supported shape and must not be flagged.
        Assert.Empty(FindProtocolCodeLiterals("Error(error, description);", ProtocolErrorCodes));
        Assert.Empty(FindProtocolCodeLiterals("""x = "content-type";""", ProtocolErrorCodes));
    }

    [Fact]
    public void PipelinesLibrary_ContainsNoProtocolErrorCode()
    {
        // DF19: the neutral project used to hardcode invalid_request, method_not_allowed, not_found and the
        // Invalid_content_type typo. Selecting a code is the core's job; here a code may only arrive as an
        // argument.
        var offenders = new List<string>();

        foreach (var (path, text) in SourceFilesOf("RoyalIdentity.Pipelines"))
        {
            foreach (var code in FindProtocolCodeLiterals(text, ProtocolErrorCodes))
                offenders.Add($"{path}: \"{code}\"");
        }

        Assert.Empty(offenders);
    }

    [Fact]
    public void Core_DoesNotExpose_AmbiguousErrorHelpers()
    {
        // DF4: InvalidRequest(description) and its two-string sibling accepted an Oidc.*.Errors.* constant in
        // the description position, which silently answered invalid_request with the real code buried in
        // error_description. The single Error(error, description) helper replaced them.
        string[] forbidden = ["InvalidRequest", "InvalidGrant", "InvalidClient"];

        var helpers = Core.GetType("RoyalIdentity.Extensions.ResponseHandlerExtensions", throwOnError: true)!;
        var declared = helpers
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var name in forbidden)
            Assert.DoesNotContain(name, declared);

        Assert.Contains("Error", declared);
    }

    [Fact]
    public void ErrorConstantInDescriptionPosition_DetectsWhatItClaimsTo()
    {
        // Proof cases, positional and named. The named form was previously able to slip past.
        Assert.Matches(
            ErrorConstantInDescriptionPosition,
            "context.Error(Oidc.Authorize.Errors.InvalidRequest, Oidc.Token.Errors.InvalidScope);");
        Assert.Matches(
            ErrorConstantInDescriptionPosition,
            "context.Error(\n    \"a description\",\n    Oidc.Token.Errors.InvalidScope);");
        Assert.Matches(
            ErrorConstantInDescriptionPosition,
            "context.Error(Oidc.Token.Errors.InvalidGrant, errorDescription: Oidc.Token.Errors.InvalidScope);");
        Assert.Matches(
            ErrorConstantInDescriptionPosition,
            "context.Error(errorDescription: Oidc.Token.Errors.InvalidScope, error: Oidc.Token.Errors.InvalidGrant);");
        Assert.Matches(
            ErrorConstantInDescriptionPosition,
            "context.Error(Oidc.Token.Errors.InvalidGrant, Oidc.Errors.Revocation.UnsupportedTokenType);");
        Assert.Matches(
            ErrorConstantInDescriptionPosition,
            "context.Error(error: Oidc.Token.Errors.InvalidGrant, errorDescription: Oidc.Errors.Revocation.UnsupportedTokenType);");

        // The supported shapes: the code first, the description second, on one line or several.
        Assert.DoesNotMatch(
            ErrorConstantInDescriptionPosition,
            "context.Error(Oidc.Authorize.Errors.InvalidScope, \"Requested scopes are invalid\");");
        Assert.DoesNotMatch(
            ErrorConstantInDescriptionPosition,
            "context.Error(\n    Oidc.Authorize.Errors.InvalidScope,\n    \"Requested scope is not allowed\");");
        Assert.DoesNotMatch(
            ErrorConstantInDescriptionPosition,
            "context.Error(error: Oidc.Token.Errors.InvalidGrant, errorDescription: \"Invalid refresh token\");");
    }

    [Fact]
    public void Core_NeverPassesAnErrorConstantAsADescription()
    {
        var offenders = SourceFilesOf(CoreName)
            .Where(file => ErrorConstantInDescriptionPosition.IsMatch(file.Text))
            .Select(file => file.Path)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void Core_OwnsTheEndpointFailuresThatSelectACode()
    {
        // The counterpart of PipelinesLibrary_ContainsNoProtocolErrorCode: the factories that were removed from
        // the neutral project must exist here, or the codes simply went missing.
        var endpointErrors = Core.GetType("RoyalIdentity.Endpoints.EndpointErrors", throwOnError: true)!;
        var declared = endpointErrors
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("MethodNotAllowed", declared);
        Assert.Contains("UnsupportedMediaType", declared);
        Assert.Contains("NotFound", declared);
    }

    [Fact]
    public void EndpointErrors_DeclaresNoErrorCode()
    {
        // DF12: 405, 415 and 404 are HTTP conditions, not members of the RFC 6749 §5.2 taxonomy. They used to
        // answer with an OAuth-shaped body carrying method_not_allowed, Invalid_content_type and not_found —
        // codes no RFC defines. They now answer application/problem+json, and no code may come back here.
        var endpointErrors = Core.GetType("RoyalIdentity.Endpoints.EndpointErrors", throwOnError: true)!;

        Assert.Empty(ConstStringsOf(endpointErrors));
    }

    [Fact]
    public void RetiredPseudoCodes_DoNotComeBack()
    {
        var offenders = new List<string>();

        foreach (var project in new[] { "RoyalIdentity", "RoyalIdentity.Pipelines" })
        {
            foreach (var (path, text) in SourceFilesOf(project))
            {
                foreach (var code in FindProtocolCodeLiterals(text, RetiredPseudoCodes))
                    offenders.Add($"{project}/{path}: \"{code}\"");
            }
        }

        Assert.Empty(offenders);
    }
}

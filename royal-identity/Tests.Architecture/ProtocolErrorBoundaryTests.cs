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

    /// <summary>
    /// Protocol error codes. None of them may be written in the neutral project — not as the selected code,
    /// not as a fallback, not as a typo of one.
    /// </summary>
    private static readonly string[] ProtocolErrorCodes =
    [
        "invalid_request",
        "invalid_client",
        "invalid_grant",
        "invalid_scope",
        "invalid_target",
        "invalid_token",
        "unauthorized_client",
        "unsupported_grant_type",
        "unsupported_response_type",
        "unsupported_response_mode",
        "access_denied",
        "login_required",
        "consent_required",
        "interaction_required",
        "method_not_allowed",
        "not_found",
        "content_type",
    ];

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
    public void PipelinesLibrary_ContainsNoProtocolErrorCode()
    {
        // DF19: the neutral project used to hardcode invalid_request, method_not_allowed, not_found and the
        // Invalid_content_type typo. Selecting a code is the core's job; here a code may only arrive as an
        // argument.
        var offenders = new List<string>();

        foreach (var (path, text) in SourceFilesOf("RoyalIdentity.Pipelines"))
        {
            foreach (var code in ProtocolErrorCodes)
            {
                if (text.Contains($"\"{code}\"", StringComparison.OrdinalIgnoreCase))
                    offenders.Add($"{path}: \"{code}\"");
            }
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
    public void Core_NeverPassesAnErrorConstantAsADescription()
    {
        // Matches `.Error(<something>, Oidc.<Area>.Errors.<Code>` — a code in the second (description)
        // position. The first position is where a code belongs and is deliberately not matched.
        var errorInDescriptionPosition = new Regex(
            @"\.Error\(\s*[^,)]+,\s*Oidc\.[A-Za-z]+\.Errors\.",
            RegexOptions.Compiled);

        var offenders = SourceFilesOf(CoreName)
            .Where(file => errorInDescriptionPosition.IsMatch(file.Text))
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
}

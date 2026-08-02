using System.Reflection;
using RoyalIdentity;
using RoyalIdentity.Contexts.Items;
using RoyalIdentity.Contracts;
using RoyalIdentity.Contracts.Models;
using RoyalIdentity.Events;
using RoyalIdentity.Users.Contexts;
using RoyalIdentity.Utils;

namespace Tests.Architecture;

/// <summary>
/// Guards closed refactoring surfaces removed by plan-refactoring-debt-closure.
/// </summary>
public class RefactoringDebtBoundaryTests
{
    [Fact]
    public void ClosedPhaseTwoSymbols_DoNotReturn()
    {
        var offenders = SourceFiles()
            .Where(file => file.Text.Contains("AuthenticationProperties" + "Extensions", StringComparison.Ordinal)
                || file.Text.Contains("GenerateCodeChallenge" + "S256", StringComparison.Ordinal)
                || file.Text.Contains("public string? " + "IdP", StringComparison.Ordinal)
                || file.Text.Contains("Troca o tipo de " + "retorno", StringComparison.Ordinal)
                || file.Text.Contains("Acredito que o uso destes", StringComparison.Ordinal))
            .Select(file => file.Path)
            .ToArray();

        Assert.Empty(offenders);
        Assert.Null(typeof(AuthorizationContext).GetProperty("Id" + "P"));
    }

    [Fact]
    public void ClientSecretChecker_ExposesTheSettledEvaluatedClientContract()
    {
        var method = typeof(IClientSecretChecker).GetMethod(
            nameof(IClientSecretChecker.EvaluateClientAsync));

        Assert.NotNull(method);
        Assert.Equal(typeof(Task<EvaluatedClient>), method.ReturnType);
        Assert.Empty(method.GetCustomAttributes<RedesignAttribute>());
    }

    [Fact]
    public void PkceHelper_ExposesOnlyTheThreeDistinctSettledOperations()
    {
        var methods = typeof(PkceHelper)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                nameof(PkceHelper.GenerateS256CodeChallenge),
                nameof(PkceHelper.GenerateStoredS256CodeChallengeHash),
                nameof(PkceHelper.HashCodeChallengeForStorage),
            ],
            methods);
    }

    [Fact]
    public void IssuedTokenEvents_ContinueToExposeTheObfuscatedTokenModel()
    {
        Assert.Empty(typeof(Token).GetCustomAttributes<RedesignAttribute>());

        var eventTokenProperties = new[]
        {
            typeof(AccessTokenIssuedEvent).GetProperty(nameof(AccessTokenIssuedEvent.AccessToken)),
            typeof(IdentityTokenIssuedEvent).GetProperty(nameof(IdentityTokenIssuedEvent.IdentityToken)),
            typeof(RefreshTokenIssuedEvent).GetProperty(nameof(RefreshTokenIssuedEvent.RefreshToken)),
            typeof(CodeIssuedEvent).GetProperty(nameof(CodeIssuedEvent.Code)),
        };

        Assert.All(eventTokenProperties, property =>
        {
            Assert.NotNull(property);
            Assert.Equal(typeof(Token), property.PropertyType);
        });
    }

    [Fact]
    public void OrphanEndpointProject_RemainsRemovedAndCoverageRemainsInPipelines()
    {
        var root = ProjectReferenceReader.FindRepositoryRoot();

        Assert.False(Directory.Exists(Path.Combine(root, "Tests.Endpoints")));

        var pipelineTest = File.ReadAllText(Path.Combine(
            root,
            "Tests.Pipelines",
            "ServerEndpointTests.cs"));
        Assert.Contains("EndpointHandler_Must_CreateResponse", pipelineTest, StringComparison.Ordinal);
    }

    private static IEnumerable<(string Path, string Text)> SourceFiles()
    {
        var project = Path.Combine(ProjectReferenceReader.FindRepositoryRoot(), "RoyalIdentity");

        return Directory
            .EnumerateFiles(project, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Select(path => (
                Path.GetRelativePath(project, path).Replace('\\', '/'),
                File.ReadAllText(path)));
    }
}

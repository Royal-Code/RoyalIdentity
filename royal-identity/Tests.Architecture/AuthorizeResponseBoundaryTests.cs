namespace Tests.Architecture;

/// <summary>
/// Guards the bounded Authentication Response construction surface introduced by
/// plan-oidc-session-management DF24.
/// </summary>
public class AuthorizeResponseBoundaryTests
{
    [Fact]
    public void AuthenticationResponses_AreConstructedOnlyByTheFactory()
    {
        var constructions = SourceFiles()
            .Where(file => file.Text.Contains("new AuthorizeResponse(", StringComparison.Ordinal)
                || file.Text.Contains("new AuthorizeErrorResponse(", StringComparison.Ordinal))
            .Select(file => file.Path)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(["Responses/AuthorizeResponseFactory.cs"], constructions);
    }

    [Fact]
    public void SessionStateGenerator_IsInvokedOnlyByTheFactory()
    {
        var invocations = SourceFiles()
            .Where(file => file.Text.Contains(
                "sessionStateGenerator.GenerateSessionStateValue(",
                StringComparison.Ordinal))
            .Select(file => file.Path)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(["Responses/AuthorizeResponseFactory.cs"], invocations);
    }

    [Fact]
    public void ResponseFactory_IsConsumedOnlyByTheBoundedAuthenticationResponseCallers()
    {
        var callers = SourceFiles()
            .Where(file => file.Text.Contains("AuthorizeResponseFactory.", StringComparison.Ordinal))
            .Select(file => file.Path)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "Contexts/Decorators/ConsentDecorator.cs",
                "Contexts/Decorators/PromptLoginDecorator.cs",
                "Contexts/Validators/AuthorizeMainValidator.cs",
                "Handlers/AuthorizeHandler.cs",
            ],
            callers);
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

using System.Reflection;
using RoyalIdentity.Handlers;
using RoyalIdentity.Responses;

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
                ".GenerateSessionStateValue(",
                StringComparison.Ordinal))
            .Select(file => file.Path)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(["Responses/AuthorizeResponseFactory.cs"], invocations);
    }

    [Fact]
    public void AuthenticationResponseConstructors_AreNotPublic()
    {
        Assert.Empty(typeof(AuthorizeResponse).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(typeof(AuthorizeErrorResponse).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void AuthorizationResponseBoundary_CannotIssueFrontChannelTokens()
    {
        var responseProperties = typeof(AuthorizeResponse)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name)
            .ToArray();
        var handlerConstructorParameters = Assert.Single(typeof(AuthorizeHandler).GetConstructors())
            .GetParameters()
            .Select(parameter => parameter.ParameterType.Name)
            .ToArray();

        Assert.DoesNotContain("Token", responseProperties);
        Assert.DoesNotContain("IdentityToken", responseProperties);
        Assert.DoesNotContain("TokenType", responseProperties);
        Assert.DoesNotContain("AccessTokenLifetime", responseProperties);
        Assert.DoesNotContain("ITokenFactory", handlerConstructorParameters);
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
                "Contexts/Decorators/PromptNoneInteractionDecorator.cs",
                "Contexts/Validators/AuthorizeMainValidator.cs",
                "Handlers/AuthorizeHandler.cs",
            ],
            callers);
    }

    [Fact]
    public void PromptNoneInteractionDecorator_WrapsBuiltInAndCustomizedInteractionProducers()
    {
        var pipes = Assert.Single(SourceFiles(), file => file.Path == "Pipes.cs").Text;
        var blockStart = pipes.IndexOf(
            "var authorizeContextPipe = builder.For<AuthorizeContext>()",
            StringComparison.Ordinal);

        Assert.True(blockStart >= 0, "The AuthorizeContext pipeline was not found in Pipes.cs.");

        var blockEnd = pipes.IndexOf(
            "var authorizeValidateContextPipe = builder.For<AuthorizeValidateContext>()",
            blockStart,
            StringComparison.Ordinal);

        Assert.True(blockEnd > blockStart, "The end of the AuthorizeContext pipeline was not found in Pipes.cs.");

        var authorizePipe = pipes[blockStart..blockEnd];
        var redirectValidator = authorizePipe.IndexOf(
            ".UseValidator<RedirectUriValidator>()",
            StringComparison.Ordinal);
        var interactionBoundary = authorizePipe.IndexOf(
            ".UseDecorator<PromptNoneInteractionDecorator>()",
            StringComparison.Ordinal);
        var loginProducer = authorizePipe.IndexOf(
            ".UseDecorator<PromptLoginDecorator>()",
            StringComparison.Ordinal);
        var consentProducer = authorizePipe.IndexOf(
            ".UseDecorator<ConsentDecorator>()",
            StringComparison.Ordinal);
        var customProducers = authorizePipe.IndexOf(
            "options.CustomizeAuthorizeContext?.Invoke(authorizeContextPipe)",
            StringComparison.Ordinal);

        Assert.True(redirectValidator >= 0, "RedirectUriValidator is missing from the AuthorizeContext pipeline.");
        Assert.True(interactionBoundary > redirectValidator,
            "PromptNoneInteractionDecorator must run after redirect URI validation.");
        Assert.Equal(
            interactionBoundary,
            authorizePipe.LastIndexOf(
                ".UseDecorator<PromptNoneInteractionDecorator>()",
                StringComparison.Ordinal));
        Assert.True(loginProducer > interactionBoundary,
            "PromptNoneInteractionDecorator must wrap PromptLoginDecorator.");
        Assert.True(consentProducer > interactionBoundary,
            "PromptNoneInteractionDecorator must wrap ConsentDecorator.");
        Assert.True(customProducers > interactionBoundary,
            "PromptNoneInteractionDecorator must wrap CustomizeAuthorizeContext components.");
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

using RoyalIdentity.Contexts;
using RoyalIdentity.Contexts.Decorators;
using RoyalIdentity.Contexts.Validators;
using RoyalIdentity.Contexts.Withs;
using RoyalIdentity.Handlers;
using RoyalIdentity.Pipelines.Abstractions;

namespace Tests.Architecture;

/// <summary>
/// Prevents pipeline components from losing their public composition surface or being narrowed from a reusable
/// capability contract to one concrete context merely to serve a specialized branch.
/// </summary>
public class PipelineComponentContractTests
{
    public static TheoryData<Type, Type> CapabilityContracts => new()
    {
        { typeof(PromptLoginDecorator), typeof(IDecorator<IWithPrompt>) },
        { typeof(EvaluateClient), typeof(IDecorator<IWithClient>) },
        { typeof(LoadClient), typeof(IDecorator<IWithClient>) },
        { typeof(ResourcesDecorator), typeof(IDecorator<IWithResources>) },
        { typeof(AuthorizeMainValidator), typeof(IValidator<IAuthorizationContextBase>) },
        { typeof(AuthorizationResourcesValidator), typeof(IValidator<IAuthorizationContextBase>) },
        { typeof(PkceValidator), typeof(IValidator<IWithCodeChallenge>) },
        { typeof(RedirectUriValidator), typeof(IValidator<IWithRedirectUri>) },
        { typeof(ResourcesValidator), typeof(IValidator<IWithResources>) },
        { typeof(ActiveUserValidator), typeof(IValidator<ITokenEndpointContextBase>) },
        { typeof(GrantTypeValidator), typeof(IValidator<ITokenEndpointContextBase>) },
    };

    [Theory]
    [MemberData(nameof(CapabilityContracts))]
    public void ReusableComponents_KeepTheirCapabilityBasedContract(Type component, Type contract)
    {
        Assert.Contains(contract, component.GetInterfaces());
    }

    [Fact]
    public void RawPromptValues_BelongToThePromptCapabilityContract()
    {
        var property = typeof(IWithPrompt).GetProperty(nameof(IWithPrompt.RequestedPromptModes));

        Assert.NotNull(property);
        Assert.Equal(typeof(HashSet<string>), property.PropertyType);
        Assert.True(property.CanRead);
    }

    [Fact]
    public void CorePipelineComponents_ArePubliclyComposable()
    {
        var openContracts = new[]
        {
            typeof(IDecorator<>),
            typeof(IValidator<>),
            typeof(IHandler<>),
        };
        var offenders = typeof(AuthorizeHandler).Assembly
            .GetTypes()
            .Where(type => type is { IsAbstract: false, IsClass: true })
            .Where(type => type.GetInterfaces().Any(candidate =>
                candidate.IsGenericType && openContracts.Contains(candidate.GetGenericTypeDefinition())))
            .Where(type => !type.IsPublic)
            .Select(type => type.FullName)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }
}

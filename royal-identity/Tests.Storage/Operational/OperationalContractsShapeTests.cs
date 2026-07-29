using System.Reflection;
using RoyalIdentity.Contracts.Storage;
using RoyalIdentity.Models;
using RoyalIdentity.Models.Tokens;
using RoyalIdentity.Storage.EntityFramework.Operational.Stores;

namespace Tests.Storage.Operational;

/// <summary>
/// Guards the definitive Operational contract shape after the transitional in-memory backing was removed.
/// Atomic code consumption and conditional refresh transitions are mandatory base-store operations; there is
/// no optional capability or non-atomic update surface left for a composition to select accidentally.
/// </summary>
public class OperationalContractsShapeTests
{
    [Fact]
    public void AuthorizationCodeStore_RequiresAtomicConsumption()
    {
        var method = typeof(IAuthorizationCodeStore)
            .GetMethod(nameof(IAuthorizationCodeStore.ConsumeAuthorizationCodeAsync));

        Assert.NotNull(method);
        Assert.True(method!.IsAbstract);
        Assert.Equal(typeof(Task<AuthorizationCode?>), method.ReturnType);
        Assert.Equal(
            [typeof(string), typeof(string), typeof(string), typeof(CancellationToken)],
            method.GetParameters().Select(parameter => parameter.ParameterType));
    }

    [Fact]
    public void RefreshTokenStore_RequiresOnlyConditionalTransitions()
    {
        var methods = typeof(IRefreshTokenStore)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        Assert.DoesNotContain(methods, method => method.Name == "UpdateAsync");
        Assert.All(
            methods.Where(method => method.Name is nameof(IRefreshTokenStore.TryConsumeAsync)
                or nameof(IRefreshTokenStore.TryUpdateAsync)),
            method => Assert.True(method.IsAbstract));
        Assert.Contains(methods, method => method.Name == nameof(IRefreshTokenStore.TryConsumeAsync));
        Assert.Contains(methods, method => method.Name == nameof(IRefreshTokenStore.TryUpdateAsync));
    }

    [Fact]
    public void OperationalStoreFactory_ReturnsTheBaseStoreContracts()
    {
        var codeStore = typeof(IOperationalStoreFactory)
            .GetMethod(nameof(IOperationalStoreFactory.GetAuthorizationCodeStore))!.ReturnType;
        var refreshStore = typeof(IOperationalStoreFactory)
            .GetMethod(nameof(IOperationalStoreFactory.GetRefreshTokenStore))!.ReturnType;

        Assert.Equal(typeof(IAuthorizationCodeStore), codeStore);
        Assert.Equal(typeof(IRefreshTokenStore), refreshStore);
    }

    [Fact]
    public void Storage_ExposesAuthorizeParameters_OnlyThroughARealmAccessor()
    {
        Assert.Null(typeof(IStorage).GetProperty("AuthorizeParameters"));

        var accessor = typeof(IStorage).GetMethod(nameof(IStorage.GetAuthorizeParametersStore));

        Assert.NotNull(accessor);
        Assert.Equal(typeof(IAuthorizeParametersStore), accessor!.ReturnType);
        Assert.Equal(typeof(Realm), Assert.Single(accessor.GetParameters()).ParameterType);
    }
}

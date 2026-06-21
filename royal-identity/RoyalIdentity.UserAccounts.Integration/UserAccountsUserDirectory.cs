using RoyalIdentity.Models;
using RoyalIdentity.UserAccounts.Features.Accounts.Commons;
using RoyalIdentity.UserAccounts.Features.Accounts.UseCases;
using RoyalIdentity.UserAccounts.Features.ScopeProperties.Commons;
using RoyalIdentity.Users.Contracts;

namespace RoyalIdentity.UserAccounts.Integration;

/// <summary>
/// Module-backed <see cref="IUserDirectory"/> (ADR-015 §2.1): the dedicated gateway that binds each edge port to
/// a realm at construction and hands it back without a realm parameter. It is the only place that translates the
/// rich IdP <see cref="Realm"/> into the module's primitive <c>RealmId</c> + <see cref="UserAccountsRealmBinding"/>
/// (via <see cref="UserAccountsRealmBindingFactory"/>); the ports it returns never see <see cref="Realm"/>.
/// <para>
/// Scoped: it consumes the module's scoped collaborators (reader, claims reader, authenticate handler) from the
/// current request scope. This replaces the in-memory fake gateway as an opt-in DI swap.
/// </para>
/// </summary>
public sealed class UserAccountsUserDirectory(
    UserAccountReader reader,
    UserAccountClaimsReader claimsReader,
    IAuthenticateLocalCredentialHandler authenticate,
    UserAccountsRealmBindingFactory bindingFactory) : IUserDirectory
{
    /// <inheritdoc />
    public ISubjectStore GetSubjectStore(Realm realm)
    {
        var binding = bindingFactory.Create(realm);
        return new SubjectStore(reader, binding.RealmId);
    }

    /// <inheritdoc />
    public ILocalUserAuthenticator GetLocalAuthenticator(Realm realm)
    {
        var binding = bindingFactory.Create(realm);
        return new LocalUserAuthenticator(authenticate, binding.RealmId, binding.Options);
    }

    /// <inheritdoc />
    public IUserClaimsProvider GetClaimsProvider(Realm realm)
    {
        var binding = bindingFactory.Create(realm);
        return new UserClaimsProvider(claimsReader, binding.RealmId, binding.Options);
    }
}

using RoyalIdentity.Contracts.Storage;

namespace RoyalIdentity.Storage.EntityFramework.Operational.Stores;

/// <summary>
/// <para>
///     The authorization-code store this adapter produces: the CRUD contract <b>and</b> the MP-2 capability.
/// </para>
/// <para>
///     This is how the EF composition satisfies "the registration validates the presence of the capability"
///     (plan DF39) — as a compile-time guarantee rather than a runtime check. An EF store that did not provide
///     <see cref="ISingleUseAuthorizationCodeStore"/> could not be returned by
///     <see cref="IOperationalStoreFactory"/> at all, so the transitional fallback of
///     <see cref="IAuthorizationCodeConsumer"/> is unreachable from this adapter by construction. Only the
///     in-memory fake, which never implements the capability, can reach it (ADR-018).
/// </para>
/// </summary>
public interface IOperationalAuthorizationCodeStore : IAuthorizationCodeStore, ISingleUseAuthorizationCodeStore;

/// <summary>
/// The refresh-token store this adapter produces: the CRUD contract <b>and</b> the MP-3 capability, under the
/// same construction rule as <see cref="IOperationalAuthorizationCodeStore"/>.
/// </summary>
public interface IOperationalRefreshTokenStore : IRefreshTokenStore, IVersionedRefreshTokenStore;

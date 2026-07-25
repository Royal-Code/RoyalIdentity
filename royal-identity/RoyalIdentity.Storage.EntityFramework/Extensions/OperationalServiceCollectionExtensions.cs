using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using RoyalIdentity.Storage.EntityFramework.Operational;
using RoyalIdentity.Storage.EntityFramework.Operational.Materialization;
using RoyalIdentity.Storage.EntityFramework.Operational.Protection;

namespace RoyalIdentity.Storage.EntityFramework.Extensions;

public static class OperationalServiceCollectionExtensions
{
	/// <summary>
	/// <para>
	///     Registers the scoped Operational context seam and the stateless materialization services of the EF
	///     adapter over <typeparamref name="TContext"/> (plan DF1/DF2). The context itself is registered by the
	///     consumer, exactly as for Configuration, and may be a different context — with a different
	///     connection, even a different database — or one combined context applying both mapping extensions.
	/// </para>
	/// <para>
	///     This registration never provides <c>IStorage</c>, <c>IStorageProvider</c> or
	///     <c>IStorageSession</c> — the complete gateway is an explicit opt-in composition of both families
	///     (plan DF21) — and never applies migrations (plan DF23). Payload protection profiles are registered
	///     separately and deliberately: no profile is implied here, so a realm selecting an unregistered
	///     profile fails closed instead of silently writing unprotected data (plan DF30).
	/// </para>
	/// </summary>
	public static IServiceCollection AddEntityFrameworkOperationalStorage<TContext>(this IServiceCollection services)
		where TContext : DbContext
	{
		ArgumentNullException.ThrowIfNull(services);

		services.TryAddSingleton(TimeProvider.System);
		services.TryAddSingleton<OperationalLookupDigest>();
		services.TryAddSingleton<AccessTokenPayloadSerializer>();
		services.TryAddSingleton<RefreshTokenPayloadSerializer>();
		services.TryAddSingleton<AuthorizationCodePayloadSerializer>();
		services.TryAddSingleton<ConsentPayloadSerializer>();
		services.TryAddSingleton<AuthorizeParametersPayloadSerializer>();
		services.TryAddSingleton<OperationalPayloadProtectorResolver>();
		services.TryAddSingleton<OperationalPayloadProtection>();
		services.TryAddScoped<IOperationalDbContextAccessor, OperationalDbContextAccessor<TContext>>();

		return services;
	}

	/// <summary>
	/// Registers an AES-GCM operational payload protection profile under <paramref name="profileId"/>. The key
	/// belongs to the composition (KMS, vault, or another secure source) and never enters the Configuration
	/// payload — realms only ever store the profile id (plan DF30).
	/// </summary>
	public static IServiceCollection AddOperationalAesGcmPayloadProtection(
		this IServiceCollection services, string profileId, byte[] key)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(profileId);
		ArgumentNullException.ThrowIfNull(key);

		services.AddSingleton<IOperationalPayloadProtector>(
			_ => new AesGcmOperationalPayloadProtector(profileId, key));

		return services;
	}

	/// <summary>
	/// Registers an ASP.NET Core Data Protection operational payload profile under
	/// <paramref name="profileId"/>. Production compositions must configure a persistent, shared key ring when
	/// more than one instance reads the same operational database.
	/// </summary>
	public static IServiceCollection AddOperationalDataProtectionPayloadProtection(
		this IServiceCollection services, string profileId)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(profileId);

		services.AddSingleton<IOperationalPayloadProtector>(provider =>
			new DataProtectionOperationalPayloadProtector(
				profileId, provider.GetRequiredService<IDataProtectionProvider>()));

		return services;
	}

	/// <summary>
	/// Registers an unprotected operational payload profile under <paramref name="profileId"/>. It is never a
	/// fallback: registering it here and selecting its id on a realm are two independent, deliberate opt-ins,
	/// and it logs a warning when constructed (plan DF30).
	/// </summary>
	public static IServiceCollection AddOperationalPlainPayloadProtection(
		this IServiceCollection services, string profileId)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentException.ThrowIfNullOrWhiteSpace(profileId);

		services.AddSingleton<IOperationalPayloadProtector>(provider =>
			new PlainOperationalPayloadProtector(
				profileId, provider.GetRequiredService<ILogger<PlainOperationalPayloadProtector>>()));

		return services;
	}
}

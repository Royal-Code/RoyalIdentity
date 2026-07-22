using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RoyalIdentity.Storage.EntityFramework.Security.KeyMaterial;

namespace RoyalIdentity.Storage.EntityFramework.Extensions;

public static class KeyMaterialServiceCollectionExtensions
{
	/// <summary>
	/// Explicitly opts into unencrypted signing-key material. Intended only for development, tests or an
	/// operator-accepted environment; resolving the protector emits a security warning.
	/// </summary>
	public static IServiceCollection AddPlainKeyMaterialProtector(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);
		services.AddLogging();
		services.RemoveAll<IKeyMaterialProtector>();
		services.AddSingleton<IKeyMaterialProtector, PlainKeyMaterialProtector>();
		return services;
	}

	/// <summary>
	/// Uses ASP.NET Core Data Protection. The consumer remains responsible for configuring a persistent,
	/// shared key ring and protecting that ring appropriately for its deployment.
	/// </summary>
	public static IServiceCollection AddAspNetDataProtectionKeyMaterialProtector(this IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);
		services.AddDataProtection();
		services.RemoveAll<IKeyMaterialProtector>();
		services.AddSingleton<IKeyMaterialProtector, AspNetDataProtectionKeyMaterialProtector>();
		return services;
	}

	/// <summary>Uses AES-GCM with key material supplied entirely by the consumer.</summary>
	public static IServiceCollection AddAesKeyMaterialProtector(
		this IServiceCollection services,
		Action<AesKeyMaterialProtectorOptions> configure)
	{
		ArgumentNullException.ThrowIfNull(services);
		ArgumentNullException.ThrowIfNull(configure);
		services.Configure(configure);
		services.RemoveAll<IKeyMaterialProtector>();
		services.AddSingleton<IKeyMaterialProtector, AesKeyMaterialProtector>();
		return services;
	}
}
